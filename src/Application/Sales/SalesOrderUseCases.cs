using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Numbering;
using Domain.MasterData.Branches;
using Domain.MasterData.Customers;
using Domain.MasterData.Items;
using Domain.MasterData.TaxCodes;
using Domain.Sales.SalesOrders;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Sales;

/// <param name="EstimatedWeightKg">Expected total live weight of the birds on the line.</param>
/// <param name="PricePerKg">Price per kg live weight, excluding VAT.</param>
public sealed record SalesOrderLineRequest(Guid ItemId, int Birds, decimal EstimatedWeightKg, decimal PricePerKg, Guid? TaxCodeId);

public sealed record CreateSalesOrderCommand(
    Guid BranchId,
    Guid CustomerId,
    DateOnly OrderDate,
    DateOnly? DeliveryDate,
    string? Notes,
    IReadOnlyList<SalesOrderLineRequest> Lines) : ICommand<CreateSalesOrderResponse>;

public sealed record CreateSalesOrderResponse(Guid Id, string Number);

public sealed record UpdateSalesOrderCommand(
    Guid SalesOrderId,
    DateOnly OrderDate,
    DateOnly? DeliveryDate,
    string? Notes,
    IReadOnlyList<SalesOrderLineRequest> Lines) : ICommand;

/// <summary>
/// Approves a draft order after the credit check. With <paramref name="CreditOverrideReason"/> the order may exceed
/// the customer's credit limit (endpoint guarded by <c>sales:credit-override</c>).
/// </summary>
public sealed record ApproveSalesOrderCommand(Guid SalesOrderId, string? CreditOverrideReason) : ICommand;

public sealed record CancelSalesOrderCommand(Guid SalesOrderId, string Reason) : ICommand;

public sealed record CloseSalesOrderCommand(Guid SalesOrderId) : ICommand;

internal sealed class SalesOrderLineRequestValidator : AbstractValidator<SalesOrderLineRequest>
{
    public SalesOrderLineRequestValidator()
    {
        RuleFor(l => l.ItemId).NotEmpty();
        RuleFor(l => l.Birds).GreaterThan(0);
        RuleFor(l => l.EstimatedWeightKg).GreaterThan(0);
        RuleFor(l => l.PricePerKg).GreaterThan(0);
    }
}

internal sealed class CreateSalesOrderCommandValidator : AbstractValidator<CreateSalesOrderCommand>
{
    public CreateSalesOrderCommandValidator()
    {
        RuleFor(c => c.BranchId).NotEmpty();
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.Notes).MaximumLength(1000);
        RuleFor(c => c.Lines).NotEmpty();
        RuleForEach(c => c.Lines).SetValidator(new SalesOrderLineRequestValidator());
    }
}

internal sealed class UpdateSalesOrderCommandValidator : AbstractValidator<UpdateSalesOrderCommand>
{
    public UpdateSalesOrderCommandValidator()
    {
        RuleFor(c => c.SalesOrderId).NotEmpty();
        RuleFor(c => c.Notes).MaximumLength(1000);
        RuleFor(c => c.Lines).NotEmpty();
        RuleForEach(c => c.Lines).SetValidator(new SalesOrderLineRequestValidator());
    }
}

internal sealed class ApproveSalesOrderCommandValidator : AbstractValidator<ApproveSalesOrderCommand>
{
    public ApproveSalesOrderCommandValidator()
    {
        RuleFor(c => c.SalesOrderId).NotEmpty();
        RuleFor(c => c.CreditOverrideReason).NotEmpty().MaximumLength(500).When(c => c.CreditOverrideReason is not null);
    }
}

internal sealed class CancelSalesOrderCommandValidator : AbstractValidator<CancelSalesOrderCommand>
{
    public CancelSalesOrderCommandValidator()
    {
        RuleFor(c => c.SalesOrderId).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(500);
    }
}

internal static class SalesOrderSupport
{
    public const string DocumentPrefix = "SO";

    /// <summary>
    /// Every line must sell an active live bird item; the VAT code, if any, must be an active VAT code.
    /// </summary>
    public static async Task<Result<List<SalesOrderLineInput>>> ToDomainAsync(
        IApplicationDbContext context,
        IReadOnlyList<SalesOrderLineRequest> lines,
        CancellationToken cancellationToken)
    {
        var itemIds = lines.Select(l => l.ItemId).Distinct().ToList();
        List<Guid> sellable = await context.Items
            .Where(i => itemIds.Contains(i.Id) && i.IsActive && i.Category == ItemCategory.LiveBird)
            .Select(i => i.Id)
            .ToListAsync(cancellationToken);

        if (itemIds.Exists(id => !sellable.Contains(id)))
        {
            return Result.Failure<List<SalesOrderLineInput>>(SalesOrderErrors.ItemNotSellable);
        }

        var taxCodeIds = lines.Where(l => l.TaxCodeId is not null).Select(l => l.TaxCodeId!.Value).Distinct().ToList();
        List<Guid> validTaxCodes = await context.TaxCodes
            .Where(t => taxCodeIds.Contains(t.Id) && t.Type == TaxType.Vat && t.IsActive)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        Guid invalidTaxCode = taxCodeIds.Find(id => !validTaxCodes.Contains(id));
        if (invalidTaxCode != Guid.Empty)
        {
            return Result.Failure<List<SalesOrderLineInput>>(TaxCodeErrors.NotFound(invalidTaxCode));
        }

        return lines
            .Select(l => new SalesOrderLineInput(l.ItemId, l.Birds, l.EstimatedWeightKg, new Money(l.PricePerKg), l.TaxCodeId))
            .ToList();
    }
}

internal sealed class CreateSalesOrderCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDocumentNumberGenerator numberGenerator) : ICommandHandler<CreateSalesOrderCommand, CreateSalesOrderResponse>
{
    public async Task<Result<CreateSalesOrderResponse>> Handle(CreateSalesOrderCommand command, CancellationToken cancellationToken)
    {
        Result access = await branchAccess.EnsureAccessAsync(command.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<CreateSalesOrderResponse>(access.Error);
        }

        if (!await context.Branches.AnyAsync(b => b.Id == command.BranchId && b.IsActive, cancellationToken))
        {
            return Result.Failure<CreateSalesOrderResponse>(BranchErrors.NotFound(command.BranchId));
        }

        if (!await context.Customers.AnyAsync(c => c.Id == command.CustomerId && c.IsActive, cancellationToken))
        {
            return Result.Failure<CreateSalesOrderResponse>(CustomerErrors.NotFound(command.CustomerId));
        }

        Result<List<SalesOrderLineInput>> lines = await SalesOrderSupport.ToDomainAsync(context, command.Lines, cancellationToken);
        if (lines.IsFailure)
        {
            return Result.Failure<CreateSalesOrderResponse>(lines.Error);
        }

        // Validate with a placeholder number so a rejected order does not consume a document number.
        Result<SalesOrder> validation = SalesOrder.Create(
            string.Empty, command.BranchId, command.CustomerId, command.OrderDate, command.DeliveryDate, command.Notes, lines.Value);

        if (validation.IsFailure)
        {
            return Result.Failure<CreateSalesOrderResponse>(validation.Error);
        }

        string number = await numberGenerator.NextForBranchAsync(
            context, SalesOrderSupport.DocumentPrefix, command.BranchId, command.OrderDate, cancellationToken);

        SalesOrder order = SalesOrder.Create(
            number, command.BranchId, command.CustomerId, command.OrderDate, command.DeliveryDate, command.Notes, lines.Value).Value;

        context.SalesOrders.Add(order);

        await context.SaveChangesAsync(cancellationToken);

        return new CreateSalesOrderResponse(order.Id, order.Number);
    }
}

internal sealed class UpdateSalesOrderCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<UpdateSalesOrderCommand>
{
    public async Task<Result> Handle(UpdateSalesOrderCommand command, CancellationToken cancellationToken)
    {
        Result<SalesOrder> order = await SalesSupport.LoadOrderAsync(context, branchAccess, command.SalesOrderId, cancellationToken);
        if (order.IsFailure)
        {
            return order;
        }

        Result<List<SalesOrderLineInput>> lines = await SalesOrderSupport.ToDomainAsync(context, command.Lines, cancellationToken);
        if (lines.IsFailure)
        {
            return lines;
        }

        Result result = order.Value.Update(command.OrderDate, command.DeliveryDate, command.Notes, lines.Value);
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class ApproveSalesOrderCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<ApproveSalesOrderCommand>
{
    public async Task<Result> Handle(ApproveSalesOrderCommand command, CancellationToken cancellationToken)
    {
        Result<SalesOrder> order = await SalesSupport.LoadOrderAsync(context, branchAccess, command.SalesOrderId, cancellationToken);
        if (order.IsFailure)
        {
            return order;
        }

        Customer? customer = await context.Customers.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == order.Value.CustomerId, cancellationToken);

        if (customer is null || !customer.IsActive)
        {
            return Result.Failure(CustomerErrors.NotFound(order.Value.CustomerId));
        }

        Money exposure = await SalesSupport.CreditExposureAsync(context, customer.Id, order.Value.Id, cancellationToken);

        Result result = order.Value.Approve(
            userContext.UserId,
            dateTimeProvider.UtcNow,
            new CreditCheck(customer.CreditLimit, exposure),
            command.CreditOverrideReason);

        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class CancelSalesOrderCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<CancelSalesOrderCommand>
{
    public async Task<Result> Handle(CancelSalesOrderCommand command, CancellationToken cancellationToken)
    {
        Result<SalesOrder> order = await SalesSupport.LoadOrderAsync(context, branchAccess, command.SalesOrderId, cancellationToken);
        if (order.IsFailure)
        {
            return order;
        }

        Result result = order.Value.Cancel(command.Reason);
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class CloseSalesOrderCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<CloseSalesOrderCommand>
{
    public async Task<Result> Handle(CloseSalesOrderCommand command, CancellationToken cancellationToken)
    {
        Result<SalesOrder> order = await SalesSupport.LoadOrderAsync(context, branchAccess, command.SalesOrderId, cancellationToken);
        if (order.IsFailure)
        {
            return order;
        }

        Result result = order.Value.Close();
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
