using System.ComponentModel.DataAnnotations;
using Application.Approvals;
using Domain.Approvals;
using Microsoft.AspNetCore.Mvc.Rendering;
using Web.App.Areas.Approvals.Models;

namespace Web.App.Areas.Admin.Models;

public sealed record ApprovalFlowListViewModel(
    IReadOnlyList<ApprovalFlowResponse> Flows,
    ApprovalDocumentType? DocumentType,
    string? Branch,
    IReadOnlyList<SelectListItem> BranchOptions);

public sealed class ApprovalFlowFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [Display(Name = "Document type")]
    public ApprovalDocumentType? DocumentType { get; set; }

    /// <summary>
    /// Empty for the default flow of every branch.
    /// </summary>
    [Display(Name = "Branch")]
    public Guid? BranchId { get; set; }

    [StringLength(150)]
    public string? Name { get; set; }

    [Display(Name = "Active")]
    public bool? IsActive { get; set; }

    public List<ApprovalLevelRow> Levels { get; set; } = [];

    public IReadOnlyList<SelectListItem> BranchOptions { get; set; } = [];

    public IReadOnlyDictionary<Guid, string> ApproverNames { get; set; } = new Dictionary<Guid, string>();
}

public sealed record ReplaceApproverViewModel(
    Guid? FromUserId,
    Guid? ToUserId,
    Guid? BranchId,
    ApproverUsageResponse? Usage,
    IReadOnlyList<SelectListItem> FromOptions,
    IReadOnlyList<SelectListItem> ToOptions,
    IReadOnlyList<SelectListItem> BranchOptions);
