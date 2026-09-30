using SharedKernel;

namespace Domain.Finance.JournalTemplates;

public static class JournalTemplateErrors
{
    public static Error NotFound(Guid templateId) => Error.NotFound(
        "JournalTemplates.NotFound",
        $"The journal template with the Id = '{templateId}' was not found");

    public static readonly Error NameNotUnique = Error.Conflict(
        "JournalTemplates.NameNotUnique",
        "A journal template with this name already exists");

    public static readonly Error NeedsBothSides = Error.Problem(
        "JournalTemplates.NeedsBothSides",
        "A template needs at least one debit line and one credit line");
}
