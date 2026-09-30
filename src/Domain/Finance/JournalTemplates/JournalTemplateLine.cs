using Domain.Finance.Accounts;

namespace Domain.Finance.JournalTemplates;

public sealed class JournalTemplateLine
{
    internal JournalTemplateLine(
        Guid journalTemplateId,
        int lineNumber,
        Guid accountId,
        Guid? costCenterId,
        BalanceSide side,
        string? description)
    {
        JournalTemplateId = journalTemplateId;
        LineNumber = lineNumber;
        AccountId = accountId;
        CostCenterId = costCenterId;
        Side = side;
        Description = description;
    }

    private JournalTemplateLine()
    {
    }

    public Guid JournalTemplateId { get; private set; }
    public int LineNumber { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid? CostCenterId { get; private set; }
    public BalanceSide Side { get; private set; }
    public string? Description { get; private set; }
}
