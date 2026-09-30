namespace Domain.Finance.JournalMappings;

public sealed class JournalMappingLine
{
    internal JournalMappingLine(
        Guid journalMappingId,
        string component,
        Guid debitAccountId,
        Guid creditAccountId,
        Guid? costCenterId)
    {
        JournalMappingId = journalMappingId;
        Component = component;
        DebitAccountId = debitAccountId;
        CreditAccountId = creditAccountId;
        CostCenterId = costCenterId;
    }

    private JournalMappingLine()
    {
    }

    public Guid JournalMappingId { get; private set; }
    public string Component { get; private set; }
    public Guid DebitAccountId { get; private set; }
    public Guid CreditAccountId { get; private set; }
    public Guid? CostCenterId { get; private set; }
}
