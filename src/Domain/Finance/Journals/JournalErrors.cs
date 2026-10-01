using SharedKernel;

namespace Domain.Finance.Journals;

public static class JournalErrors
{
    public static Error NotFound(Guid journalId) => Error.NotFound(
        "Journals.NotFound",
        $"The journal with the Id = '{journalId}' was not found");

    public static Error NotEditable(Guid journalId) => Error.Problem(
        "Journals.NotEditable",
        $"The journal with the Id = '{journalId}' is not a manual draft and cannot be changed or deleted");

    public static Error InvalidTransition(JournalStatus from, JournalStatus to) => Error.Problem(
        "Journals.InvalidTransition",
        $"A journal cannot move from {from} to {to}");

    public static Error NotBalanced(Money debit, Money credit) => Error.Problem(
        "Journals.NotBalanced",
        $"Total debit ({debit}) must equal total credit ({credit})");

    public static readonly Error TooFewLines = Error.Problem(
        "Journals.TooFewLines",
        $"A journal needs at least {JournalEntry.MinimumLines} lines");

    public static readonly Error InvalidLineAmount = Error.Problem(
        "Journals.InvalidLineAmount",
        "Each line must have either a positive debit or a positive credit, not both");

    public static readonly Error SelfApprovalNotAllowed = Error.Problem(
        "Journals.SelfApprovalNotAllowed",
        "A journal must be approved by someone other than its creator");

    public static Error SourceNotJournaled(string sourceType, Guid sourceId) => Error.NotFound(
        "Journals.SourceNotJournaled",
        $"No automatic journal exists for {sourceType} '{sourceId}'");

    public static readonly Error ReversalBeforeOriginal = Error.Problem(
        "Journals.ReversalBeforeOriginal",
        "A reversal cannot be dated before the journal it reverses");
}
