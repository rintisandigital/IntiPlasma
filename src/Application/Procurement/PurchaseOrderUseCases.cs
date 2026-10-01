using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Numbering;
using Application.Documents;
using Application.Inventory;
using Domain.Documents.Attachments;
using Domain.MasterData.Branches;
using Domain.MasterData.Items;
using Domain.MasterData.TaxCodes;
using Domain.MasterData.Vendors;
using Domain.Procurement.PurchaseOrders;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Procurement;

public sealed record PurchaseOrderLineRequest(Guid ItemId, Guid UomId, decimal Quantity, decimal UnitPrice, Guid? TaxCodeId);

public sealed record CreatePurchaseOrderCommand(
    Guid BranchId,
    Guid VendorId,
    DateOnly OrderDate,
    DateOnly? ExpectedDate,
    string? Notes,
    IReadOnlyList<PurchaseOrderLineRequest> Lines,
    IReadOnlyList<Guid>? Documents = null) : ICommand<CreatePurchaseOrderResponse>;

public sealed record CreatePurchaseOrderResponse(Guid Id, string Number);

public sealed record UpdatePurchaseOrderCommand(
    Guid PurchaseOrderId,
    DateOnly OrderDate,
    DateOnly? ExpectedDate,
    string? Notes,
    IReadOnlyList<PurchaseOrderLineRequest> Lines,
    IReadOnlyList<Guid>? Documents = null) : ICommand;

public sealed record ApprovePurchaseOrderCommand(Guid PurchaseOrderId) : ICommand;

public sealed record CancelPurchaseOrderCommand(Guid PurchaseOrderId, string Reason) : ICommand;

public sealed record ClosePurchaseOrderCommand(Guid PurchaseOrderId) : ICommand;

internal sealed class PurchaseOrderLineRequestValidator : AbstractValidator<PurchaseOrderLineRequest>
{
    public PurchaseOrderLineRequestValidator()
    {
        RuleFor(l => l.ItemId).NotEmpty();
        RuleFor(l => l.UomId).NotEmpty();
        RuleFor(l => l.Quantity).GreaterThan(0);
        RuleFor(l => l.UnitPrice).GreaterThan(0);
    }
}

internal sealed class CreatePurchaseOrderCommandValidator : AbstractValidator<CreatePurchaseOrderCommand>
{
    public CreatePurchaseOrderCommandValidator()
    {
        RuleFor(c => c.BranchId).NotEmpty();
        RuleFor(c => c.VendorId).NotEmpty();
        RuleFor(c => c.Notes).MaximumLength(1000);
        RuleFor(c => c.Lines).NotEmpty();
        RuleForEach(c => c.Lines).SetValidator(new PurchaseOrderLineRequestValidator());
        RuleFor(c => c.Documents).ValidDocuments();
    }
}

internal sealed class UpdatePurchaseOrderCommandValidator : AbstractValidator<UpdatePurchaseOrderCommand>
{
    public UpdatePurchaseOrderCommandValidator()
    {
        RuleFor(c => c.PurchaseOrderId).NotEmpty();
        RuleFor(c => c.Notes).MaximumLength(1000);
        RuleFor(c => c.Lines).NotEmpty();
        RuleForEach(c => c.Lines).SetValidator(new PurchaseOrderLineRequestValidator());
        RuleFor(c => c.Documents).ValidDocuments();
    }
}

internal sealed class CancelPurchaseOrderCommandValidator : AbstractValidator<CancelPurchaseOrderCommand>
{
    public CancelPurchaseOrderCommandValidator()
    {
        RuleFor(c => c.PurchaseOrderId).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(500);
    }
}

internal static class PurchaseOrderSupport
{
    public const string DocumentPrefix = "PO";

    /// <summary>
    /// Every line must order an active sapronak item in its base unit or one of its conversion units;
    /// the VAT code, if any, must be a VAT code.
    /// </summary>
    public static async Task<Result<List<PurchaseOrderLineInput>>> ToDomainAsync(
        IApplicationDbContext context,
        IReadOnlyList<PurchaseOrderLineRequest> lines,
        CancellationToken cancellationToken)
    {
        Dictionary<Guid, Item> items = await InventorySupport.LoadItemsAsync(context, lines.Select(l => l.ItemId), cancellationToken);

        foreach (PurchaseOrderLineRequest line in lines)
        {
            if (!items.TryGetValue(line.ItemId, out Item? item))
            {
                return Result.Failure<List<PurchaseOrderLineInput>>(ItemErrors.NotFound(line.ItemId));
            }

            if (!item.IsActive || !InventorySupport.SapronakCategories.Contains(item.Category))
            {
                return Result.Failure<List<PurchaseOrderLineInput>>(PurchaseOrderErrors.ItemNotPurchasable);
            }

            Result<decimal> conversion = item.ConvertToBase(line.UomId, line.Quantity);
            if (conversion.IsFailure)
            {
                return Result.Failure<List<PurchaseOrderLineInput>>(conversion.Error);
            }
        }

        var taxCodeIds = lines.Where(l => l.TaxCodeId is not null).Select(l => l.TaxCodeId!.Value).Distinct().ToList();
        List<Guid> validTaxCodes = await context.TaxCodes
            .Where(t => taxCodeIds.Contains(t.Id) && t.Type == TaxType.Vat && t.IsActive)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        Guid invalidTaxCode = taxCodeIds.Find(id => !validTaxCodes.Contains(id));
        if (invalidTaxCode != Guid.Empty)
        {
            return Result.Failure<List<PurchaseOrderLineInput>>(TaxCodeErrors.NotFound(invalidTaxCode));
        }

        return lines
            .Select(l => new PurchaseOrderLineInput(l.ItemId, l.UomId, l.Quantity, new Money(l.UnitPrice), l.TaxCodeId))
            .ToList();
    }

    public static async Task<Result<PurchaseOrder>> LoadAsync(
        IApplicationDbContext context,
        IBranchAccess branchAccess,
        Guid purchaseOrderId,
        CancellationToken cancellationToken)
    {
        PurchaseOrder? order = await context.PurchaseOrders
            .Include(o => o.Lines)
            .SingleOrDefaultAsync(o => o.Id == purchaseOrderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<PurchaseOrder>(PurchaseOrderErrors.NotFound(purchaseOrderId));
        }

        Result access = await branchAccess.EnsureAccessAsync(order.BranchId, cancellationToken);

        return access.IsSuccess ? order : Result.Failure<PurchaseOrder>(access.Error);
    }
}

internal sealed class CreatePurchaseOrderCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDocumentNumberGenerator numberGenerator,
    IAttachmentService attachments) : ICommandHandler<CreatePurchaseOrderCommand, CreatePurchaseOrderResponse>
{
    public async Task<Result<CreatePurchaseOrderResponse>> Handle(CreatePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        Result access = await branchAccess.EnsureAccessAsync(command.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<CreatePurchaseOrderResponse>(access.Error);
        }

        if (!await context.Branches.AnyAsync(b => b.Id == command.BranchId && b.IsActive, cancellationToken))
        {
            return Result.Failure<CreatePurchaseOrderResponse>(BranchErrors.NotFound(command.BranchId));
        }

        if (!await context.Vendors.AnyAsync(v => v.Id == command.VendorId && v.IsActive, cancellationToken))
        {
            return Result.Failure<CreatePurchaseOrderResponse>(VendorErrors.NotFound(command.VendorId));
        }

        Result<List<PurchaseOrderLineInput>> lines = await PurchaseOrderSupport.ToDomainAsync(context, command.Lines, cancellationToken);
        if (lines.IsFailure)
        {
            return Result.Failure<CreatePurchaseOrderResponse>(lines.Error);
        }

        // Validate with a placeholder number so a rejected order does not consume a document number.
        Result<PurchaseOrder> validation = PurchaseOrder.Create(
            string.Empty, command.BranchId, command.VendorId, command.OrderDate, command.ExpectedDate, command.Notes, lines.Value);

        if (validation.IsFailure)
        {
            return Result.Failure<CreatePurchaseOrderResponse>(validation.Error);
        }

        Result attachable = await attachments.EnsureAttachableAsync(command.BranchId, command.Documents, cancellationToken);
        if (attachable.IsFailure)
        {
            return Result.Failure<CreatePurchaseOrderResponse>(attachable.Error);
        }

        string number = await numberGenerator.NextForBranchAsync(
            context, PurchaseOrderSupport.DocumentPrefix, command.BranchId, command.OrderDate, cancellationToken);

        PurchaseOrder order = PurchaseOrder.Create(
            number, command.BranchId, command.VendorId, command.OrderDate, command.ExpectedDate, command.Notes, lines.Value).Value;

        Result documents = await attachments.ApplyDocumentsAsync(
            order,
            AttachmentOwner.Of(AttachmentOwnerTypes.PurchaseOrder, order.Id),
            order.BranchId,
            command.Documents,
            cancellationToken);
        if (documents.IsFailure)
        {
            return Result.Failure<CreatePurchaseOrderResponse>(documents.Error);
        }

        context.PurchaseOrders.Add(order);

        await context.SaveChangesAsync(cancellationToken);

        return new CreatePurchaseOrderResponse(order.Id, order.Number);
    }
}

internal sealed class UpdatePurchaseOrderCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IAttachmentService attachments)
    : ICommandHandler<UpdatePurchaseOrderCommand>
{
    public async Task<Result> Handle(UpdatePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        Result<PurchaseOrder> order = await PurchaseOrderSupport.LoadAsync(context, branchAccess, command.PurchaseOrderId, cancellationToken);
        if (order.IsFailure)
        {
            return order;
        }

        Result<List<PurchaseOrderLineInput>> lines = await PurchaseOrderSupport.ToDomainAsync(context, command.Lines, cancellationToken);
        if (lines.IsFailure)
        {
            return lines;
        }

        Result result = order.Value.Update(command.OrderDate, command.ExpectedDate, command.Notes, lines.Value);
        if (result.IsFailure)
        {
            return result;
        }

        Result documents = await attachments.ApplyDocumentsAsync(
            order.Value,
            AttachmentOwner.Of(AttachmentOwnerTypes.PurchaseOrder, order.Value.Id),
            order.Value.BranchId,
            command.Documents,
            cancellationToken);
        if (documents.IsFailure)
        {
            return documents;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class ApprovePurchaseOrderCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<ApprovePurchaseOrderCommand>
{
    public async Task<Result> Handle(ApprovePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        Result<PurchaseOrder> order = await PurchaseOrderSupport.LoadAsync(context, branchAccess, command.PurchaseOrderId, cancellationToken);
        if (order.IsFailure)
        {
            return order;
        }

        Result result = order.Value.Approve(userContext.UserId, dateTimeProvider.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class CancelPurchaseOrderCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<CancelPurchaseOrderCommand>
{
    public async Task<Result> Handle(CancelPurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        Result<PurchaseOrder> order = await PurchaseOrderSupport.LoadAsync(context, branchAccess, command.PurchaseOrderId, cancellationToken);
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

internal sealed class ClosePurchaseOrderCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<ClosePurchaseOrderCommand>
{
    public async Task<Result> Handle(ClosePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        Result<PurchaseOrder> order = await PurchaseOrderSupport.LoadAsync(context, branchAccess, command.PurchaseOrderId, cancellationToken);
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
