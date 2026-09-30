namespace Domain.Finance.Journals;

public enum JournalSource
{
    /// <summary>
    /// Jurnal umum entered by the finance team.
    /// </summary>
    Manual = 1,

    /// <summary>
    /// Produced by the auto journal engine from an operational transaction.
    /// </summary>
    Automatic = 2
}

public enum JournalStatus
{
    Draft = 1,
    Approved = 2,
    Posted = 3,

    /// <summary>
    /// Posted, then cancelled by a reversal journal. Both journals stay in the ledger.
    /// </summary>
    Reversed = 4
}
