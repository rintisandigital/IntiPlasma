using Domain.Finance.Journals;
using SharedKernel;

namespace Domain.Finance.JournalMappings;

/// <summary>
/// Auto journal engine configuration: for one accounting event (optionally for one branch), which debit and
/// credit account each amount component is posted to. A branch-specific mapping overrides the default mapping
/// (<see cref="BranchId"/> = null).
/// </summary>
public sealed class JournalMapping : AggregateRoot
{
    private readonly List<JournalMappingLine> _lines = [];

    private JournalMapping(Guid id, string eventType, Guid? branchId)
        : base(id)
    {
        EventType = eventType;
        BranchId = branchId;
        IsActive = true;
    }

    private JournalMapping()
    {
    }

    public string EventType { get; private set; }
    public Guid? BranchId { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public IReadOnlyCollection<JournalMappingLine> Lines => [.. _lines];

    public static Result<JournalMapping> Create(
        string eventType,
        Guid? branchId,
        string? description,
        IReadOnlyList<(string Component, Guid DebitAccountId, Guid CreditAccountId, Guid? CostCenterId)> lines)
    {
        if (AccountingEvents.Find(eventType) is null)
        {
            return Result.Failure<JournalMapping>(JournalMappingErrors.UnknownEvent(eventType));
        }

        var mapping = new JournalMapping(Guid.CreateVersion7(), eventType, branchId);

        Result result = mapping.Update(description, isActive: true, lines);

        return result.IsSuccess ? mapping : Result.Failure<JournalMapping>(result.Error);
    }

    public Result Update(
        string? description,
        bool isActive,
        IReadOnlyList<(string Component, Guid DebitAccountId, Guid CreditAccountId, Guid? CostCenterId)> lines)
    {
        AccountingEventDefinition definition = AccountingEvents.Find(EventType)!;

        string? unknown = lines.Select(l => l.Component).FirstOrDefault(c => !definition.HasComponent(c));
        if (unknown is not null)
        {
            return Result.Failure(JournalMappingErrors.UnknownComponent(EventType, unknown));
        }

        if (lines.GroupBy(l => l.Component).Any(g => g.Count() > 1))
        {
            return Result.Failure(JournalMappingErrors.DuplicateComponent);
        }

        if (lines.Any(l => l.DebitAccountId == l.CreditAccountId))
        {
            return Result.Failure(JournalMappingErrors.SameDebitAndCredit);
        }

        Description = description;
        IsActive = isActive;

        _lines.Clear();
        _lines.AddRange(lines.Select(l => new JournalMappingLine(
            Id, l.Component, l.DebitAccountId, l.CreditAccountId, l.CostCenterId)));

        return Result.Success();
    }

    /// <summary>
    /// Turns the amounts of an accounting event into balanced journal lines: each non-zero component becomes one
    /// debit and one credit line. A negative amount swaps the sides (e.g. a stock adjustment down).
    /// Per-transaction account overrides (e.g. the actual bank account) take precedence over the mapping.
    /// </summary>
    public Result<IReadOnlyList<JournalLineInput>> BuildLines(IReadOnlyList<AccountingAmount> amounts)
    {
        var lines = new List<JournalLineInput>();
        AccountingEventDefinition definition = AccountingEvents.Find(EventType)!;

        string? unknown = amounts.Select(a => a.Component).FirstOrDefault(c => !definition.HasComponent(c));
        if (unknown is not null)
        {
            return Result.Failure<IReadOnlyList<JournalLineInput>>(JournalMappingErrors.UnknownComponent(EventType, unknown));
        }

        foreach (AccountingAmount amount in amounts.Where(a => !a.Amount.IsZero))
        {
            JournalMappingLine? mappingLine = _lines.Find(l => l.Component == amount.Component);
            if (mappingLine is null)
            {
                return Result.Failure<IReadOnlyList<JournalLineInput>>(
                    JournalMappingErrors.ComponentNotMapped(EventType, amount.Component));
            }

            Guid debitAccountId = amount.DebitAccountId ?? mappingLine.DebitAccountId;
            Guid creditAccountId = amount.CreditAccountId ?? mappingLine.CreditAccountId;
            Guid? costCenterId = amount.CostCenterId ?? mappingLine.CostCenterId;

            if (amount.Amount.IsNegative)
            {
                (debitAccountId, creditAccountId) = (creditAccountId, debitAccountId);
            }

            Money value = amount.Amount.IsNegative ? -amount.Amount : amount.Amount;

            lines.Add(JournalLineInput.DebitLine(debitAccountId, value, costCenterId, amount.Description));
            lines.Add(JournalLineInput.CreditLine(creditAccountId, value, costCenterId, amount.Description));
        }

        return lines;
    }
}
