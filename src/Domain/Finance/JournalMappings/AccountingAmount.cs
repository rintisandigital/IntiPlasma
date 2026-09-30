using SharedKernel;

namespace Domain.Finance.JournalMappings;

/// <summary>
/// One amount component of an accounting event, e.g. ("FeedReceived", Rp 12.500.000).
/// </summary>
/// <param name="DebitAccountId">Overrides the mapped debit account for this transaction (e.g. the bank used).</param>
/// <param name="CreditAccountId">Overrides the mapped credit account for this transaction.</param>
public sealed record AccountingAmount(
    string Component,
    Money Amount,
    Guid? DebitAccountId = null,
    Guid? CreditAccountId = null,
    Guid? CostCenterId = null,
    string? Description = null);
