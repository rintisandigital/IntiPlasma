using System.Globalization;
using Domain.Finance.Payables;
using Domain.MasterData.TaxCodes;
using Domain.Partnership.Contracts;
using Domain.Partnership.Cycles;
using SharedKernel;

namespace Domain.Costing.PlasmaSettlements;

/// <summary>
/// Settlement (perhitungan hasil) of a closed plasma cycle: the plasma's income by the contract scheme
/// (<see cref="ISettlementPolicy"/>), the income tax withheld per contract and a deduction of the plasma's debt.
/// Draft (recalculable) → Approved by someone other than the creator (journaled, cycle Settled) → (Partially) Paid
/// through payment vouchers. A negative result (rugi) is not paid but becomes the plasma's debt (piutang plasma).
/// </summary>
public sealed class PlasmaSettlement : AggregateRoot, IPayable
{
    private readonly List<PlasmaSettlementLine> _lines = [];

    private PlasmaSettlement(Guid id)
        : base(id)
    {
    }

    private PlasmaSettlement()
    {
    }

    public string Number { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid CycleId { get; private set; }
    public Guid FarmerId { get; private set; }
    public Guid ContractId { get; private set; }
    public ContractScheme Scheme { get; private set; }
    public DateOnly SettlementDate { get; private set; }
    public PlasmaSettlementStatus Status { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>
    /// Plasma's income before tax and deductions: the sum of the lines. Negative = loss (rugi).
    /// </summary>
    public Money GrossIncome { get; private set; } = new(0m);

    public Guid? IncomeTaxCodeId { get; private set; }
    public decimal IncomeTaxRatePercent { get; private set; }
    public Money IncomeTaxAmount { get; private set; } = new(0m);

    /// <summary>
    /// Potongan hutang plasma (e.g. a previous cycle's deficit), deducted from what is paid.
    /// </summary>
    public Money DebtDeduction { get; private set; } = new(0m);

    /// <summary>
    /// Paid to the plasma: gross income − income tax − debt deduction (zero for a loss).
    /// </summary>
    public Money NetPayable { get; private set; } = new(0m);

    /// <summary>
    /// The loss of a negative settlement, booked as piutang plasma.
    /// </summary>
    public Money Deficit { get; private set; } = new(0m);

    public Money PaidAmount { get; private set; } = new(0m);
    public Guid? ApprovedBy { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }
    public IReadOnlyCollection<PlasmaSettlementLine> Lines => [.. _lines];

    public Money Outstanding => NetPayable - PaidAmount;

    public bool IsPayable => Status is PlasmaSettlementStatus.Approved or PlasmaSettlementStatus.PartiallyPaid && !Outstanding.IsZero;

    Guid IPayable.PayeeId => FarmerId;

    string? IPayable.Number => Number;

    DateOnly IPayable.DocumentDate => SettlementDate;

    /// <param name="incomeTaxCode">The contract's PPh code with its rates, if the contract withholds income tax.</param>
    public static Result<PlasmaSettlement> Create(
        string number,
        ProductionCycle cycle,
        DateOnly settlementDate,
        SettlementInput input,
        TaxCode? incomeTaxCode,
        Money debtDeduction,
        string? notes)
    {
        if (cycle.Status != CycleStatus.Closed)
        {
            return Result.Failure<PlasmaSettlement>(PlasmaSettlementErrors.CycleNotClosed(cycle.Id, cycle.Status));
        }

        if (cycle.ContractSnapshot is null || cycle.ContractId is null)
        {
            return Result.Failure<PlasmaSettlement>(PlasmaSettlementErrors.NotAPlasmaCycle);
        }

        var settlement = new PlasmaSettlement(Guid.CreateVersion7())
        {
            Number = number,
            BranchId = cycle.BranchId,
            CycleId = cycle.Id,
            FarmerId = cycle.FarmerId,
            ContractId = cycle.ContractId.Value,
            Scheme = cycle.ContractSnapshot.Scheme,
            Status = PlasmaSettlementStatus.Draft
        };

        Result calculated = settlement.Calculate(cycle, settlementDate, input, incomeTaxCode, debtDeduction, notes);

        return calculated.IsSuccess ? settlement : Result.Failure<PlasmaSettlement>(calculated.Error);
    }

    /// <summary>
    /// Recalculates a draft, e.g. after correcting a recording or with another date or debt deduction.
    /// </summary>
    public Result Recalculate(
        ProductionCycle cycle,
        DateOnly settlementDate,
        SettlementInput input,
        TaxCode? incomeTaxCode,
        Money debtDeduction,
        string? notes)
    {
        if (Status != PlasmaSettlementStatus.Draft)
        {
            return Result.Failure(PlasmaSettlementErrors.InvalidTransition(Status, PlasmaSettlementStatus.Draft));
        }

        return Calculate(cycle, settlementDate, input, incomeTaxCode, debtDeduction, notes);
    }

    /// <summary>
    /// Approval by someone other than the creator: the settlement is journaled and the cycle becomes Settled.
    /// </summary>
    public Result Approve(Guid approverId, DateTime utcNow, ProductionCycle cycle)
    {
        if (Status != PlasmaSettlementStatus.Draft)
        {
            return Result.Failure(PlasmaSettlementErrors.InvalidTransition(Status, PlasmaSettlementStatus.Approved));
        }

        if (CreatedBy == approverId)
        {
            return Result.Failure(PlasmaSettlementErrors.SelfApprovalNotAllowed);
        }

        Result settled = cycle.MarkSettled();
        if (settled.IsFailure)
        {
            return settled;
        }

        Status = NetPayable.IsZero ? PlasmaSettlementStatus.Paid : PlasmaSettlementStatus.Approved;
        ApprovedBy = approverId;
        ApprovedAtUtc = utcNow;

        Raise(new PlasmaSettlementApprovedDomainEvent(Id));

        return Result.Success();
    }

    public Result Cancel(string reason)
    {
        if (Status != PlasmaSettlementStatus.Draft)
        {
            return Result.Failure(PlasmaSettlementErrors.InvalidTransition(Status, PlasmaSettlementStatus.Cancelled));
        }

        Status = PlasmaSettlementStatus.Cancelled;
        CancellationReason = reason;

        return Result.Success();
    }

    public Result RegisterPayment(Money amount)
    {
        if (!IsPayable)
        {
            return Result.Failure(PlasmaSettlementErrors.NotPayable(Id));
        }

        if (amount.IsNegative || amount.IsZero || amount > Outstanding)
        {
            return Result.Failure(PlasmaSettlementErrors.OverPayment(Number, Outstanding));
        }

        PaidAmount += amount;
        Status = Outstanding.IsZero ? PlasmaSettlementStatus.Paid : PlasmaSettlementStatus.PartiallyPaid;

        return Result.Success();
    }

    private Result Calculate(
        ProductionCycle cycle,
        DateOnly settlementDate,
        SettlementInput input,
        TaxCode? incomeTaxCode,
        Money debtDeduction,
        string? notes)
    {
        if (settlementDate < cycle.ClosedDate)
        {
            return Result.Failure(PlasmaSettlementErrors.BeforeClosing(cycle.ClosedDate!.Value));
        }

        Result<IReadOnlyList<SettlementLineInput>> lines = SettlementPolicies.For(Scheme).Calculate(input);
        if (lines.IsFailure)
        {
            return lines;
        }

        Money gross = lines.Value.Aggregate(new Money(0m), (total, line) => total + line.Amount);

        TaxCalculation tax = TaxCalculation.None;
        if (incomeTaxCode is not null && !gross.IsNegative)
        {
            Result<TaxCalculation> calculated = incomeTaxCode.Calculate(gross, settlementDate);
            if (calculated.IsFailure)
            {
                return calculated;
            }

            tax = calculated.Value;
        }

        Money payableBeforeDeduction = gross.IsNegative ? Money.Zero : gross - tax.TaxAmount;
        if (debtDeduction.IsNegative || debtDeduction > payableBeforeDeduction)
        {
            return Result.Failure(PlasmaSettlementErrors.InvalidDebtDeduction(payableBeforeDeduction));
        }

        SettlementDate = settlementDate;
        Notes = notes;
        GrossIncome = gross;
        IncomeTaxCodeId = incomeTaxCode?.Id;
        IncomeTaxRatePercent = tax.RatePercent;
        IncomeTaxAmount = tax.TaxAmount with { };
        DebtDeduction = debtDeduction with { };
        NetPayable = payableBeforeDeduction - debtDeduction;
        Deficit = gross.IsNegative ? -gross : new Money(0m);

        _lines.Clear();
        _lines.AddRange(lines.Value.Select((l, index) => new PlasmaSettlementLine(Id, index + 1, l)));

        return Result.Success();
    }
}

public sealed class PlasmaSettlementLine
{
    internal PlasmaSettlementLine(Guid plasmaSettlementId, int lineNumber, SettlementLineInput input)
    {
        PlasmaSettlementId = plasmaSettlementId;
        LineNumber = lineNumber;
        Type = input.Type;
        Description = input.Description;
        Quantity = input.Quantity;
        UnitPrice = input.UnitPrice?.Amount;
        Amount = input.Amount with { };
    }

    private PlasmaSettlementLine()
    {
    }

    public Guid PlasmaSettlementId { get; private set; }
    public int LineNumber { get; private set; }
    public SettlementLineType Type { get; private set; }
    public string Description { get; private set; }
    public decimal? Quantity { get; private set; }
    public decimal? UnitPrice { get; private set; }

    /// <summary>
    /// Effect on the plasma's income (negative for sapronak and penalties).
    /// </summary>
    public Money Amount { get; private set; }
}

public enum PlasmaSettlementStatus
{
    Draft = 1,
    Approved = 2,
    PartiallyPaid = 3,
    Paid = 4,
    Cancelled = 9
}

public sealed record PlasmaSettlementApprovedDomainEvent(Guid PlasmaSettlementId) : DomainEvent;

public static class PlasmaSettlementErrors
{
    public static Error NotFound(Guid plasmaSettlementId) => Error.NotFound(
        "PlasmaSettlements.NotFound",
        $"The plasma settlement with the Id = '{plasmaSettlementId}' was not found");

    public static Error InvalidTransition(PlasmaSettlementStatus from, PlasmaSettlementStatus to) => Error.Problem(
        "PlasmaSettlements.InvalidTransition",
        $"A plasma settlement cannot move from {from} to {to}");

    public static Error CycleNotClosed(Guid cycleId, CycleStatus status) => Error.Problem(
        "PlasmaSettlements.CycleNotClosed",
        $"The production cycle with the Id = '{cycleId}' is {status}; only a closed cycle can be settled");

    public static Error AlreadySettled(Guid cycleId) => Error.Conflict(
        "PlasmaSettlements.AlreadySettled",
        $"The production cycle with the Id = '{cycleId}' already has a settlement");

    public static Error BeforeClosing(DateOnly closedDate) => Error.Problem(
        "PlasmaSettlements.BeforeClosing",
        $"The settlement date cannot be before the cycle's closing date {closedDate:yyyy-MM-dd}");

    public static Error NoLiveBirdPrice(decimal averageWeightKg) => Error.Problem(
        "PlasmaSettlements.NoLiveBirdPrice",
        string.Create(CultureInfo.InvariantCulture, $"The contract has no guaranteed price for an average body weight of {averageWeightKg:0.000} kg"));

    public static Error NoInputPrice(string itemCode) => Error.Problem(
        "PlasmaSettlements.NoInputPrice",
        $"The contract has no price for sapronak item {itemCode}");

    public static Error InvalidDebtDeduction(Money maximum) => Error.Problem(
        "PlasmaSettlements.InvalidDebtDeduction",
        string.Create(CultureInfo.InvariantCulture, $"The debt deduction must be between 0 and the payable amount {maximum}"));

    public static Error NotPayable(Guid plasmaSettlementId) => Error.Problem(
        "PlasmaSettlements.NotPayable",
        $"The plasma settlement with the Id = '{plasmaSettlementId}' is not approved, has nothing to pay or is already paid");

    public static Error OverPayment(string number, Money outstanding) => Error.Problem(
        "PlasmaSettlements.OverPayment",
        string.Create(CultureInfo.InvariantCulture, $"The payment for settlement {number} must be positive and cannot exceed the outstanding {outstanding}"));

    public static readonly Error NotAPlasmaCycle = Error.Problem(
        "PlasmaSettlements.NotAPlasmaCycle",
        "Only cycles of plasma farmers (with a partnership contract) are settled");

    public static readonly Error SelfApprovalNotAllowed = Error.Problem(
        "PlasmaSettlements.SelfApprovalNotAllowed",
        "A settlement must be approved by someone other than its creator");
}
