using System.ComponentModel.DataAnnotations;
using Application.Approvals;
using Domain.Approvals;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;

namespace Web.App.Areas.Approvals.Models;

/// <summary>
/// Display texts of the approval module (UI in English, like the rest of the WebApp).
/// </summary>
public static class ApprovalLabels
{
    public static string DocumentType(string? documentType) => documentType switch
    {
        nameof(ApprovalDocumentType.Farmer) => "New farmer",
        nameof(ApprovalDocumentType.FarmerChange) => "Farmer NIK/bank change",
        nameof(ApprovalDocumentType.Coop) => "New farm",
        nameof(ApprovalDocumentType.Contract) => "Contract activation",
        null => string.Empty,
        _ => documentType
    };

    public static string Field(string field) => field switch
    {
        "Nik" => "NIK",
        "BankAccount" => "Bank account",
        _ => field
    };

    /// <summary>
    /// Page of the document a request is about.
    /// </summary>
    public static string? DocumentUrl(string documentType, Guid documentId) => documentType switch
    {
        nameof(ApprovalDocumentType.Farmer) or nameof(ApprovalDocumentType.FarmerChange) => $"/Partnership/Farmers/Details/{documentId}",
        nameof(ApprovalDocumentType.Coop) => $"/Partnership/Coops/Details/{documentId}",
        nameof(ApprovalDocumentType.Contract) => $"/Partnership/Contracts/Details/{documentId}",
        _ => null
    };

    public static IReadOnlyList<SelectListItem> DocumentTypeOptions(ApprovalDocumentType? selected = null) =>
        [.. Enum.GetValues<ApprovalDocumentType>().Select(t =>
            new SelectListItem(DocumentType(t.ToString()), t.ToString(), t == selected))];

    /// <summary>
    /// Status badge of a farmer or farm: the approval state until approved, then Active/Inactive.
    /// </summary>
    public static string RecordStatus(string registrationStatus, bool isActive)
    {
        if (registrationStatus != "Approved")
        {
            return registrationStatus;
        }

        return isActive ? "Active" : "Inactive";
    }

    public static string Level(ApprovalRequestResponse request)
    {
        if (request.Status != nameof(ApprovalStatus.Pending))
        {
            return $"{request.LevelCount} level(s)";
        }

        string level = $"Level {request.CurrentLevel} of {request.LevelCount}";

        return request.CurrentLevelName is { } name ? $"{level} · {name}" : level;
    }
}

/// <summary>
/// Model of <c>_ApprovalTimeline</c>: the requests of one document (or a single request) with their levels and
/// decisions.
/// </summary>
/// <param name="ShowActions">Shows "Decide" and "Cancel" links (not on the decision page itself).</param>
public sealed record ApprovalTimelineModel(IReadOnlyList<ApprovalDetailResponse> Requests, bool ShowActions = true);

public sealed class ApprovalListViewModel
{
    public PagedList<ApprovalRequestResponse> Rows { get; init; }

    public string? Search { get; init; }

    public string? DocumentType { get; init; }

    public string? Status { get; init; }

    public string? Branch { get; init; }

    public bool StuckOnly { get; init; }

    public IReadOnlyList<SelectListItem> BranchOptions { get; init; } = [];

    /// <summary>
    /// Pending requests per document type (inbox tabs).
    /// </summary>
    public IReadOnlyDictionary<string, int> Counts { get; init; } = new Dictionary<string, int>();
}

/// <param name="ReturnUrl">Where the decision page goes back to.</param>
public sealed record ApprovalDetailsViewModel(
    ApprovalDetailResponse Detail,
    bool CanReassign,
    IReadOnlyList<SelectListItem> Candidates,
    string ReturnUrl);

public sealed class DelegationFormViewModel
{
    /// <summary>
    /// Approver who delegates; only on the administration page (else the signed-in user).
    /// </summary>
    public Guid? DelegatorUserId { get; set; }

    [Required]
    [Display(Name = "Delegate to")]
    public Guid? DelegateUserId { get; set; }

    [Required]
    [Display(Name = "From")]
    public DateOnly? FromDate { get; set; }

    [Required]
    [Display(Name = "To")]
    public DateOnly? ToDate { get; set; }

    [StringLength(300)]
    public string? Reason { get; set; }
}

public sealed record DelegationsViewModel(
    IReadOnlyList<ApprovalDelegationResponse> Delegations,
    DelegationFormViewModel Form,
    IReadOnlyList<SelectListItem> Candidates,
    bool AllUsers,
    bool IncludeEnded,
    bool CanDelegate,
    Guid CurrentUserId);

/// <summary>
/// One level of the level editor (<c>_ApprovalLevelsEditor</c>): a free-text name and its approvers (M-96).
/// </summary>
public sealed class ApprovalLevelRow
{
    [Required]
    [StringLength(100)]
    public string? Name { get; set; }

    public List<Guid> ApproverUserIds { get; set; } = [];

    public static List<ApprovalLevelRow> From(IEnumerable<ApprovalFlowLevelResponse> levels) =>
        [.. levels.Select(l => new ApprovalLevelRow { Name = l.Name, ApproverUserIds = [.. l.Approvers.Select(a => a.UserId)] })];

    public static IReadOnlyList<ApprovalLevelDefinition> ToDefinitions(IEnumerable<ApprovalLevelRow> rows) =>
        [.. rows.Select(r => new ApprovalLevelDefinition(r.Name ?? string.Empty, r.ApproverUserIds))];
}

/// <param name="Name">Collection name posted, e.g. <c>Levels</c>.</param>
/// <param name="ApproverNames">Labels of the approvers already chosen.</param>
/// <param name="BranchField">CSS selector of the branch field the approver lookup depends on.</param>
public sealed record ApprovalLevelsEditorModel(
    string Name,
    IReadOnlyList<ApprovalLevelRow> Levels,
    IReadOnlyDictionary<Guid, string> ApproverNames,
    string? BranchField);
