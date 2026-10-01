using System.Globalization;
using SharedKernel;

namespace Domain.Common;

public static class DocumentErrors
{
    public static readonly Error Invalid = Error.Problem(
        "Documents.Invalid",
        "A document id must not be empty");

    public static readonly Error TooMany = Error.Problem(
        "Documents.TooMany",
        string.Create(CultureInfo.InvariantCulture, $"At most {DocumentList.MaxDocuments} documents can be attached"));

    /// <summary>
    /// Cancelled and voided documents are archived as they were; their attachments cannot change.
    /// </summary>
    public static readonly Error OwnerCancelled = Error.Conflict(
        "Documents.OwnerCancelled",
        "The attachments of a cancelled or voided document cannot be changed");

    public static readonly Error NotAllowed = Error.Problem(
        "Documents.NotAllowed",
        "Attachments are only allowed on manual journals");
}
