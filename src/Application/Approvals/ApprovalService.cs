using System.Text.Json;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Domain.Approvals;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Approvals;

/// <summary>
/// Submits documents to the approval module (PLAN-MOBILE §5.2). Called by the document's own command handlers; the
/// request is added to the unit of work and stored by their <c>SaveChangesAsync</c>, together with the document.
/// </summary>
public interface IApprovalService
{
    /// <summary>
    /// Picks the active flow of the document's branch, else the default flow, and copies its levels into a new
    /// pending request. Fails when no flow is configured (M-31, M-82) or the document already has a pending request.
    /// </summary>
    Task<Result<ApprovalRequest>> SubmitAsync(ApprovalSubmission submission, CancellationToken cancellationToken = default);
}

/// <param name="Changes">Old and new values shown to the approver, e.g. of a farmer change.</param>
public sealed record ApprovalSubmission(
    ApprovalDocumentType DocumentType,
    Guid DocumentId,
    Guid BranchId,
    string DocumentCode,
    string DocumentTitle,
    string? Summary,
    IReadOnlyList<ApprovalChange>? Changes = null);

/// <summary>
/// One changed field of a document under approval.
/// </summary>
public sealed record ApprovalChange(string Field, string? OldValue, string? NewValue);

internal sealed class ApprovalService(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : IApprovalService
{
    internal static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<ApprovalRequest>> SubmitAsync(
        ApprovalSubmission submission,
        CancellationToken cancellationToken = default)
    {
        bool pending = await context.ApprovalRequests.AnyAsync(
            r => r.DocumentType == submission.DocumentType
                 && r.DocumentId == submission.DocumentId
                 && r.Status == ApprovalStatus.Pending,
            cancellationToken);
        if (pending)
        {
            return Result.Failure<ApprovalRequest>(ApprovalErrors.DocumentPending);
        }

        List<ApprovalFlow> flows = await context.ApprovalFlows
            .Include(f => f.Levels)
            .Where(f => f.DocumentType == submission.DocumentType && f.IsActive
                        && (f.BranchId == submission.BranchId || f.BranchId == null))
            .ToListAsync(cancellationToken);

        ApprovalFlow? flow = flows.Find(f => f.BranchId == submission.BranchId) ?? flows.Find(f => f.BranchId == null);
        if (flow is null)
        {
            string branch = await context.Branches
                .Where(b => b.Id == submission.BranchId)
                .Select(b => b.Name)
                .SingleOrDefaultAsync(cancellationToken) ?? submission.BranchId.ToString();

            return Result.Failure<ApprovalRequest>(ApprovalErrors.FlowNotConfigured(submission.DocumentType, branch));
        }

        var request = ApprovalRequest.Submit(
            submission.DocumentType,
            submission.DocumentId,
            submission.BranchId,
            submission.DocumentCode,
            submission.DocumentTitle,
            submission.Summary,
            submission.Changes is { Count: > 0 } changes ? JsonSerializer.Serialize(changes, PayloadOptions) : null,
            flow.Id,
            flow.ToDefinitions(),
            userContext.UserId,
            dateTimeProvider.UtcNow);

        context.ApprovalRequests.Add(request);

        return request;
    }
}
