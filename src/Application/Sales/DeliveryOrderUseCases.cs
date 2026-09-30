using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Numbering;
using Application.Production;
using Domain.Sales.DeliveryOrders;
using Domain.Sales.SalesOrders;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Sales;

/// <param name="HarvestId">A harvest record (panen) of a cycle in the sales order's branch, delivered whole.</param>
public sealed record DeliveryOrderLineRequest(int SalesOrderLineNumber, Guid HarvestId);

/// <summary>
/// Delivery order (surat jalan) of harvested birds against an approved sales order.
/// </summary>
public sealed record CreateDeliveryOrderCommand(
    Guid SalesOrderId,
    DateOnly DeliveryDate,
    string? VehicleNumber,
    string? DriverName,
    string? Notes,
    IReadOnlyList<DeliveryOrderLineRequest> Lines) : ICommand<CreateDeliveryOrderResponse>;

public sealed record CreateDeliveryOrderResponse(Guid Id, string Number);

/// <summary>
/// Cancels an uninvoiced delivery order; its birds go back to outstanding on the sales order and its harvests can be
/// delivered again.
/// </summary>
public sealed record CancelDeliveryOrderCommand(Guid DeliveryOrderId, string Reason) : ICommand;

internal sealed class CreateDeliveryOrderCommandValidator : AbstractValidator<CreateDeliveryOrderCommand>
{
    public CreateDeliveryOrderCommandValidator()
    {
        RuleFor(c => c.SalesOrderId).NotEmpty();
        RuleFor(c => c.VehicleNumber).MaximumLength(20);
        RuleFor(c => c.DriverName).MaximumLength(100);
        RuleFor(c => c.Notes).MaximumLength(1000);
        RuleFor(c => c.Lines).NotEmpty();
        RuleForEach(c => c.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.SalesOrderLineNumber).GreaterThan(0);
            line.RuleFor(l => l.HarvestId).NotEmpty();
        });
    }
}

internal sealed class CancelDeliveryOrderCommandValidator : AbstractValidator<CancelDeliveryOrderCommand>
{
    public CancelDeliveryOrderCommandValidator()
    {
        RuleFor(c => c.DeliveryOrderId).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(500);
    }
}

internal sealed class CreateDeliveryOrderCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDocumentNumberGenerator numberGenerator,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<CreateDeliveryOrderCommand, CreateDeliveryOrderResponse>
{
    private const string DocumentPrefix = "DO";

    public async Task<Result<CreateDeliveryOrderResponse>> Handle(CreateDeliveryOrderCommand command, CancellationToken cancellationToken)
    {
        Result notFuture = DailyRecordingSupport.EnsureNotFuture(command.DeliveryDate, dateTimeProvider);
        if (notFuture.IsFailure)
        {
            return Result.Failure<CreateDeliveryOrderResponse>(notFuture.Error);
        }

        Result<SalesOrder> order = await SalesSupport.LoadOrderAsync(context, branchAccess, command.SalesOrderId, cancellationToken);
        if (order.IsFailure)
        {
            return Result.Failure<CreateDeliveryOrderResponse>(order.Error);
        }

        Result<List<DeliveryOrderLineInput>> lines = await ToDomainAsync(command.Lines, cancellationToken);
        if (lines.IsFailure)
        {
            return Result.Failure<CreateDeliveryOrderResponse>(lines.Error);
        }

        // Validated with a placeholder number first so a rejected delivery does not consume a document number.
        Result<DeliveryOrder> validation = DeliveryOrder.Create(
            string.Empty, order.Value, command.DeliveryDate, command.VehicleNumber, command.DriverName, command.Notes, lines.Value);

        if (validation.IsFailure)
        {
            return Result.Failure<CreateDeliveryOrderResponse>(validation.Error);
        }

        string number = await numberGenerator.NextForBranchAsync(
            context, DocumentPrefix, order.Value.BranchId, command.DeliveryDate, cancellationToken);

        DeliveryOrder delivery = DeliveryOrder.Create(
            number, order.Value, command.DeliveryDate, command.VehicleNumber, command.DriverName, command.Notes, lines.Value).Value;

        foreach (DeliveryOrderLine line in delivery.Lines)
        {
            Result registered = order.Value.RegisterDelivery(line.SalesOrderLineNumber, line.Birds, line.WeightKg);
            if (registered.IsFailure)
            {
                return Result.Failure<CreateDeliveryOrderResponse>(registered.Error);
            }
        }

        context.DeliveryOrders.Add(delivery);

        await context.SaveChangesAsync(cancellationToken);

        return new CreateDeliveryOrderResponse(delivery.Id, delivery.Number);
    }

    /// <summary>
    /// Looks up the harvests (with the branch of their cycle) and rejects the ones already on an active delivery order.
    /// </summary>
    private async Task<Result<List<DeliveryOrderLineInput>>> ToDomainAsync(
        IReadOnlyList<DeliveryOrderLineRequest> requests,
        CancellationToken cancellationToken)
    {
        var harvestIds = requests.Select(r => r.HarvestId).Distinct().ToList();

        var harvests = await context.ProductionCycles.AsNoTracking()
            .SelectMany(c => c.Harvests, (c, h) => new { h.Id, CycleId = c.Id, c.BranchId, h.Date, h.Birds, h.WeightKg })
            .Where(h => harvestIds.Contains(h.Id))
            .ToListAsync(cancellationToken);

        Guid missing = harvestIds.Find(id => harvests.TrueForAll(h => h.Id != id));
        if (missing != Guid.Empty)
        {
            return Result.Failure<List<DeliveryOrderLineInput>>(DeliveryOrderErrors.HarvestNotFound(missing));
        }

        Guid? delivered = await context.DeliveryOrders
            .SelectMany(d => d.Lines)
            .Where(l => !l.IsCancelled && harvestIds.Contains(l.HarvestId))
            .Select(l => (Guid?)l.HarvestId)
            .FirstOrDefaultAsync(cancellationToken);

        if (delivered is not null)
        {
            return Result.Failure<List<DeliveryOrderLineInput>>(DeliveryOrderErrors.HarvestAlreadyDelivered(delivered.Value));
        }

        return requests
            .Select(r =>
            {
                var h = harvests.Single(x => x.Id == r.HarvestId);
                return new DeliveryOrderLineInput(
                    r.SalesOrderLineNumber, new HarvestToDeliver(h.Id, h.CycleId, h.BranchId, h.Date, h.Birds, h.WeightKg));
            })
            .ToList();
    }
}

internal sealed class CancelDeliveryOrderCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<CancelDeliveryOrderCommand>
{
    public async Task<Result> Handle(CancelDeliveryOrderCommand command, CancellationToken cancellationToken)
    {
        Result<DeliveryOrder> delivery = await SalesSupport.LoadDeliveryAsync(context, branchAccess, command.DeliveryOrderId, cancellationToken);
        if (delivery.IsFailure)
        {
            return delivery;
        }

        Result<SalesOrder> order = await SalesSupport.LoadOrderAsync(context, branchAccess, delivery.Value.SalesOrderId, cancellationToken);
        if (order.IsFailure)
        {
            return order;
        }

        Result cancelled = delivery.Value.Cancel(command.Reason);
        if (cancelled.IsFailure)
        {
            return cancelled;
        }

        foreach (DeliveryOrderLine line in delivery.Value.Lines)
        {
            Result reversed = order.Value.ReverseDelivery(line.SalesOrderLineNumber, line.Birds, line.WeightKg);
            if (reversed.IsFailure)
            {
                return reversed;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
