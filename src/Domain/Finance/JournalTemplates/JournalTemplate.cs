using Domain.Finance.Accounts;
using SharedKernel;

namespace Domain.Finance.JournalTemplates;

/// <summary>
/// Reusable layout for recurring manual journals (e.g. monthly depreciation, payroll accrual).
/// Stores accounts and sides only; amounts are entered when the journal is created.
/// </summary>
public sealed class JournalTemplate : AggregateRoot
{
    private readonly List<JournalTemplateLine> _lines = [];

    private JournalTemplate(Guid id)
        : base(id)
    {
        IsActive = true;
    }

    private JournalTemplate()
    {
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public IReadOnlyCollection<JournalTemplateLine> Lines => [.. _lines];

    public static Result<JournalTemplate> Create(
        string name,
        string? description,
        IReadOnlyList<(Guid AccountId, Guid? CostCenterId, BalanceSide Side, string? Description)> lines)
    {
        var template = new JournalTemplate(Guid.CreateVersion7());

        Result result = template.Update(name, description, isActive: true, lines);

        return result.IsSuccess ? template : Result.Failure<JournalTemplate>(result.Error);
    }

    public Result Update(
        string name,
        string? description,
        bool isActive,
        IReadOnlyList<(Guid AccountId, Guid? CostCenterId, BalanceSide Side, string? Description)> lines)
    {
        if (!lines.Any(l => l.Side == BalanceSide.Debit) || !lines.Any(l => l.Side == BalanceSide.Credit))
        {
            return Result.Failure(JournalTemplateErrors.NeedsBothSides);
        }

        Name = name.Trim();
        Description = description;
        IsActive = isActive;

        _lines.Clear();
        _lines.AddRange(lines.Select((line, index) => new JournalTemplateLine(
            Id, index + 1, line.AccountId, line.CostCenterId, line.Side, line.Description)));

        return Result.Success();
    }
}
