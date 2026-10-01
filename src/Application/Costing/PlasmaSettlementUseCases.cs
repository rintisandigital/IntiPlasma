using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Numbering;
using Application.Documents;
using Application.Finance.AutoJournal;
using Application.Finance.Journals;
using Application.Inventory;
using Application.Production;
using Application.Sales;
using Domain.Costing.PlasmaSettlements;
using Domain.Documents.Attachments;
using Domain.Finance.JournalMappings;
using Domain.MasterData.TaxCodes;
using Domain.Partnership.Cycles;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Costing;

/// <summary>
/// Calculates the settlement of a closed plasma cycle as a draft (maker). Returns its id and number.
/// </summary>
/// <param name="DebtDeduction">Potongan hutang plasma deducted from what is paid (e.g. a previous cycle's deficit).</param>
public sealed record CreatePlasmaSettlementCommand(
    Guid CycleId,
    DateOnly SettlementDate,
    decimal DebtDeduction,
    string? Notes,
    IReadOnlyList<Guid>? Documents = null) : ICommand<CreatePlasmaSettlementResponse>;

public sealed record CreatePlasmaSettlementResponse(Guid Id, string Number);

/// <summary>
/// Recalculates a draft settlement (e.g. after a correction) with a new date, debt deduction or notes.
/// </summary>
public sealed record RecalculatePlasmaSettlementCommand(
    Guid PlasmaSettlementId,
    DateOnly SettlementDate,
    decimal DebtDeduction,
    string? Notes,
    IReadOnlyList<Guid>? Documents = null) : ICommand;

/// <summary>
/// Approval by someone other than the creator (checker): journals the settlement and locks the cycle (Settled).
/// </summary>
public sealed record ApprovePlasmaSettlementCommand(Guid PlasmaSettlementId) : ICommand;

public sealed record CancelPlasmaSettlementCommand(Guid PlasmaSettlementId, string Reason) : ICommand;

internal sealed class CreatePlasmaSettlementCommandValidator : AbstractValidator<CreatePlasmaSettlementCommand>
{
    public CreatePlasmaSettlementCommandValidator()
    {
        RuleFor(c => c.CycleId).NotEmpty();
        RuleFor(c => c.DebtDeduction).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Notes).MaximumLength(1000);
        RuleFor(c => c.Documents).ValidDocuments();
    }
}

internal sealed class RecalculatePlasmaSettlementCommandValidator : AbstractValidator<RecalculatePlasmaSettlementCommand>
{
    public RecalculatePlasmaSettlementCommandValidator()
    {
        RuleFor(c => c.PlasmaSettlementId).NotEmpty();
        RuleFor(c => c.DebtDeduction).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Notes).MaximumLength(1000);
        RuleFor(c => c.Documents).ValidDocuments();
    }
}

internal sealed class CancelPlasmaSettlementCommandValidator : AbstractValidator<CancelPlasmaSettlementCommand>
{
    public CancelPlasmaSettlementCommandValidator()
    {
        RuleFor(c => c.PlasmaSettlementId).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(500);
    }
}

internal static class PlasmaSettlementSupport
{
    public static async Task<Result<ProductionCycle>> LoadCycleAsync(
        IApplicationDbContext context,
        IBranchAccess branchAccess,
        Guid cycleId,
        CancellationToken cancellationToken)
    {
        ProductionCycle? cycle = await context.ProductionCycles
            .Include(c => c.Harvests)
            .SingleOrDefaultAsync(c => c.Id == cycleId, cancellationToken);

        if (cycle is null)
        {
            return Result.Failure<ProductionCycle>(CycleErrors.NotFound(cycleId));
        }

        Result access = await branchAccess.EnsureAccessAsync(cycle.BranchId, cancellationToken);

        return access.IsSuccess ? cycle : Result.Failure<ProductionCycle>(access.Error);
    }

    public static async Task<Result<PlasmaSettlement>> LoadAsync(
        IApplicationDbContext context,
        IBranchAccess branchAccess,
        Guid plasmaSettlementId,
        CancellationToken cancellationToken)
    {
        PlasmaSettlement? settlement = await context.PlasmaSettlements
            .Include(s => s.Lines)
            .SingleOrDefaultAsync(s => s.Id == plasmaSettlementId, cancellationToken);

        if (settlement is null)
        {
            return Result.Failure<PlasmaSettlement>(PlasmaSettlementErrors.NotFound(plasmaSettlementId));
        }

        Result access = await branchAccess.EnsureAccessAsync(settlement.BranchId, cancellationToken);

        return access.IsSuccess ? settlement : Result.Failure<PlasmaSettlement>(access.Error);
    }

    /// <summary>
    /// Gathers the closed cycle's figures for the settlement policy: performance, harvests, sapronak consumed,
    /// net sales (DPP − credit notes) and final cost.
    /// </summary>
    public static async Task<SettlementInput?> InputAsync(
        IApplicationDbContext context,
        ProductionCycle cycle,
        CancellationToken cancellationToken)
    {
        if (cycle.ContractSnapshot is null || cycle.ClosingPerformance is null)
        {
            return null;
        }

        List<ConsumedInput> inputs = await CycleCosting.ConsumedInputsAsync(context, cycle.Id, cancellationToken);

        List<decimal> sales = await context.SalesInvoices.AsNoTracking()
            .Where(i => SalesSupport.PostedInvoiceStatuses.Contains(i.Status))
            .SelectMany(i => i.Lines)
            .Where(l => l.CycleId == cycle.Id)
            .Select(l => l.Amount.Amount)
            .ToListAsync(cancellationToken);

        List<decimal> credits = await context.SalesCreditNotes.AsNoTracking()
            .SelectMany(n => n.Lines)
            .Where(l => l.CycleId == cycle.Id)
            .Select(l => l.Amount.Amount)
            .ToListAsync(cancellationToken);

        decimal cycleCost = cycle.ClosingCost?.TotalCost ?? inputs.Sum(i => i.Cost.Amount);

        return new SettlementInput(
            cycle.ContractSnapshot,
            cycle.ClosingPerformance,
            [.. cycle.Harvests.Select(h => new HarvestFigures(h.Date, h.Birds, h.WeightKg))],
            inputs,
            new Money(sales.Sum() - credits.Sum()),
            new Money(cycleCost));
    }

    public static async Task<TaxCode?> IncomeTaxCodeAsync(
        IApplicationDbContext context,
        ProductionCycle cycle,
        CancellationToken cancellationToken)
    {
        Guid? taxCodeId = cycle.ContractSnapshot?.IncomeTaxCodeId;

        return taxCodeId is null
            ? null
            : await context.TaxCodes.AsNoTracking().Include(t => t.Rates).SingleOrDefaultAsync(t => t.Id == taxCodeId, cancellationToken);
    }
}

internal sealed class CreatePlasmaSettlementCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDocumentNumberGenerator numberGenerator,
    IDateTimeProvider dateTimeProvider,
    IAttachmentService attachments) : ICommandHandler<CreatePlasmaSettlementCommand, CreatePlasmaSettlementResponse>
{
    private const string DocumentPrefix = "STL";

    public async Task<Result<CreatePlasmaSettlementResponse>> Handle(CreatePlasmaSettlementCommand command, CancellationToken cancellationToken)
    {
        Result notFuture = DailyRecordingSupport.EnsureNotFuture(command.SettlementDate, dateTimeProvider);
        if (notFuture.IsFailure)
        {
            return Result.Failure<CreatePlasmaSettlementResponse>(notFuture.Error);
        }

        Result<ProductionCycle> cycle = await PlasmaSettlementSupport.LoadCycleAsync(context, branchAccess, command.CycleId, cancellationToken);
        if (cycle.IsFailure)
        {
            return Result.Failure<CreatePlasmaSettlementResponse>(cycle.Error);
        }

        if (await context.PlasmaSettlements.AnyAsync(
                s => s.CycleId == command.CycleId && s.Status != PlasmaSettlementStatus.Cancelled, cancellationToken))
        {
            return Result.Failure<CreatePlasmaSettlementResponse>(PlasmaSettlementErrors.AlreadySettled(command.CycleId));
        }

        SettlementInput? input = await PlasmaSettlementSupport.InputAsync(context, cycle.Value, cancellationToken);
        if (input is null)
        {
            return Result.Failure<CreatePlasmaSettlementResponse>(cycle.Value.Status != CycleStatus.Closed
                ? PlasmaSettlementErrors.CycleNotClosed(cycle.Value.Id, cycle.Value.Status)
                : PlasmaSettlementErrors.NotAPlasmaCycle);
        }

        TaxCode? incomeTax = await PlasmaSettlementSupport.IncomeTaxCodeAsync(context, cycle.Value, cancellationToken);
        var deduction = new Money(command.DebtDeduction);

        // Validated with a placeholder number first so a rejected settlement does not consume a document number.
        Result<PlasmaSettlement> validation = PlasmaSettlement.Create(
            string.Empty, cycle.Value, command.SettlementDate, input, incomeTax, deduction, command.Notes);

        if (validation.IsFailure)
        {
            return Result.Failure<CreatePlasmaSettlementResponse>(validation.Error);
        }

        Result attachable = await attachments.EnsureAttachableAsync(cycle.Value.BranchId, command.Documents, cancellationToken);
        if (attachable.IsFailure)
        {
            return Result.Failure<CreatePlasmaSettlementResponse>(attachable.Error);
        }

        string number = await numberGenerator.NextForBranchAsync(
            context, DocumentPrefix, cycle.Value.BranchId, command.SettlementDate, cancellationToken);

        PlasmaSettlement settlement = PlasmaSettlement.Create(
            number, cycle.Value, command.SettlementDate, input, incomeTax, deduction, command.Notes).Value;

        Result documents = await attachments.ApplyDocumentsAsync(
            settlement,
            AttachmentOwner.Of(AttachmentOwnerTypes.PlasmaSettlement, settlement.Id),
            settlement.BranchId,
            command.Documents,
            cancellationToken);
        if (documents.IsFailure)
        {
            return Result.Failure<CreatePlasmaSettlementResponse>(documents.Error);
        }

        context.PlasmaSettlements.Add(settlement);

        await context.SaveChangesAsync(cancellationToken);

        return new CreatePlasmaSettlementResponse(settlement.Id, settlement.Number);
    }
}

internal sealed class RecalculatePlasmaSettlementCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDateTimeProvider dateTimeProvider,
    IAttachmentService attachments) : ICommandHandler<RecalculatePlasmaSettlementCommand>
{
    public async Task<Result> Handle(RecalculatePlasmaSettlementCommand command, CancellationToken cancellationToken)
    {
        Result notFuture = DailyRecordingSupport.EnsureNotFuture(command.SettlementDate, dateTimeProvider);
        if (notFuture.IsFailure)
        {
            return notFuture;
        }

        Result<PlasmaSettlement> settlement = await PlasmaSettlementSupport.LoadAsync(
            context, branchAccess, command.PlasmaSettlementId, cancellationToken);

        if (settlement.IsFailure)
        {
            return settlement;
        }

        Result<ProductionCycle> cycle = await PlasmaSettlementSupport.LoadCycleAsync(
            context, branchAccess, settlement.Value.CycleId, cancellationToken);

        if (cycle.IsFailure)
        {
            return cycle;
        }

        SettlementInput? input = await PlasmaSettlementSupport.InputAsync(context, cycle.Value, cancellationToken);
        if (input is null)
        {
            return Result.Failure(PlasmaSettlementErrors.NotAPlasmaCycle);
        }

        TaxCode? incomeTax = await PlasmaSettlementSupport.IncomeTaxCodeAsync(context, cycle.Value, cancellationToken);

        Result result = settlement.Value.Recalculate(
            cycle.Value, command.SettlementDate, input, incomeTax, new Money(command.DebtDeduction), command.Notes);

        if (result.IsFailure)
        {
            return result;
        }

        Result documents = await attachments.ApplyDocumentsAsync(
            settlement.Value,
            AttachmentOwner.Of(AttachmentOwnerTypes.PlasmaSettlement, settlement.Value.Id),
            settlement.Value.BranchId,
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

internal sealed class ApprovePlasmaSettlementCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<ApprovePlasmaSettlementCommand>
{
    public async Task<Result> Handle(ApprovePlasmaSettlementCommand command, CancellationToken cancellationToken)
    {
        Result<PlasmaSettlement> settlement = await PlasmaSettlementSupport.LoadAsync(
            context, branchAccess, command.PlasmaSettlementId, cancellationToken);

        if (settlement.IsFailure)
        {
            return settlement;
        }

        // The journal is posted from the outbox; reject a closed period now rather than dead-letter it later.
        Result<Domain.Finance.FiscalPeriods.FiscalPeriod> period = await JournalSupport.FindPeriodAsync(
            context, settlement.Value.SettlementDate, cancellationToken);

        if (period.IsFailure)
        {
            return period;
        }

        ProductionCycle cycle = await context.ProductionCycles.SingleAsync(c => c.Id == settlement.Value.CycleId, cancellationToken);

        Result result = settlement.Value.Approve(userContext.UserId, dateTimeProvider.UtcNow, cycle);
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class CancelPlasmaSettlementCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<CancelPlasmaSettlementCommand>
{
    public async Task<Result> Handle(CancelPlasmaSettlementCommand command, CancellationToken cancellationToken)
    {
        Result<PlasmaSettlement> settlement = await PlasmaSettlementSupport.LoadAsync(
            context, branchAccess, command.PlasmaSettlementId, cancellationToken);

        if (settlement.IsFailure)
        {
            return settlement;
        }

        Result result = settlement.Value.Cancel(command.Reason);
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

/// <summary>
/// Journals an approved settlement: Dr beban kemitraan / Cr hutang plasma (income), Dr hutang plasma / Cr hutang PPh
/// (tax withheld), Dr hutang plasma / Cr piutang plasma (debt deduction), and for a loss Dr piutang plasma /
/// Cr beban kemitraan.
/// </summary>
internal sealed class PlasmaSettlementApprovedDomainEventHandler(IApplicationDbContext context, IAutoJournalService autoJournal)
    : IDomainEventHandler<PlasmaSettlementApprovedDomainEvent>
{
    public async Task Handle(PlasmaSettlementApprovedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        PlasmaSettlement settlement = await context.PlasmaSettlements.AsNoTracking()
            .SingleAsync(s => s.Id == domainEvent.PlasmaSettlementId, cancellationToken);

        string farmer = await context.Farmers
            .Where(f => f.Id == settlement.FarmerId)
            .Select(f => f.Name)
            .SingleAsync(cancellationToken);

        Money income = settlement.GrossIncome.IsNegative ? Money.Zero : settlement.GrossIncome;

        await InventoryAccounting.PostAsync(autoJournal, new AccountingEntry(
            AccountingEvents.PlasmaSettlement,
            settlement.Id,
            settlement.BranchId,
            settlement.SettlementDate,
            $"Settlement {settlement.Number} plasma {farmer}",
            [
                new AccountingAmount("PlasmaIncome", income),
                new AccountingAmount("IncomeTaxWithheld", settlement.IncomeTaxAmount),
                new AccountingAmount("Deduction", settlement.DebtDeduction),
                new AccountingAmount("PlasmaDeficit", settlement.Deficit)
            ]),
            cancellationToken);
    }
}
