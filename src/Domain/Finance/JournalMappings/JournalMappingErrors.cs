using SharedKernel;

namespace Domain.Finance.JournalMappings;

public static class JournalMappingErrors
{
    public static Error NotFound(Guid mappingId) => Error.NotFound(
        "JournalMappings.NotFound",
        $"The journal mapping with the Id = '{mappingId}' was not found");

    public static Error NotConfigured(string eventType, Guid branchId) => Error.Problem(
        "JournalMappings.NotConfigured",
        $"No active journal mapping exists for event '{eventType}' (branch '{branchId}' or default)");

    public static Error UnknownEvent(string eventType) => Error.Problem(
        "JournalMappings.UnknownEvent",
        $"'{eventType}' is not a known accounting event");

    public static Error UnknownComponent(string eventType, string component) => Error.Problem(
        "JournalMappings.UnknownComponent",
        $"'{component}' is not a component of the accounting event '{eventType}'");

    public static Error ComponentNotMapped(string eventType, string component) => Error.Problem(
        "JournalMappings.ComponentNotMapped",
        $"The component '{component}' of event '{eventType}' has no account mapping");

    public static readonly Error AlreadyExists = Error.Conflict(
        "JournalMappings.AlreadyExists",
        "A mapping for this event and branch already exists");

    public static readonly Error DuplicateComponent = Error.Problem(
        "JournalMappings.DuplicateComponent",
        "A component can only be mapped once");

    public static readonly Error SameDebitAndCredit = Error.Problem(
        "JournalMappings.SameDebitAndCredit",
        "The debit and credit account of a component must differ");

    public static readonly Error NothingToPost = Error.Problem(
        "JournalMappings.NothingToPost",
        "All amounts are zero; there is nothing to journal");
}
