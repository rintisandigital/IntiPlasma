using System.Globalization;
using MobileApp.Core.Contracts;

namespace MobileApp.Core.Api;

/// <summary>
/// Modul approval berjenjang (PLAN-MOBILE M6, M-92): inbox, requests, decisions, cancelling one's own request and
/// delegations. Lists and details are cached so they can be read offline; every action needs the server (M-4).
/// </summary>
public sealed class ApprovalsApi(ApiCache cache, ApiClient api)
{
    public const int PageSize = 20;

    public Task<CachedResult<PagedList<ApprovalRequest>>> GetInboxAsync(
        string? documentType,
        int page,
        CancellationToken cancellationToken = default) =>
        cache.GetAsync<PagedList<ApprovalRequest>>(
            PartnershipApi.Path("approvals/inbox", ("documentType", documentType), ("page", Text(page)), ("pageSize", Text(PageSize))),
            cancellationToken);

    /// <summary>
    /// For the bottom bar badge; not cached (a stale count is worse than none).
    /// </summary>
    public Task<ApiResult<ApprovalInboxCount>> GetInboxCountAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<ApprovalInboxCount>("approvals/inbox/count", cancellationToken);

    public Task<CachedResult<PagedList<ApprovalRequest>>> GetMineAsync(
        string? status,
        int page,
        CancellationToken cancellationToken = default) =>
        cache.GetAsync<PagedList<ApprovalRequest>>(
            PartnershipApi.Path("approvals/mine", ("status", status), ("page", Text(page)), ("pageSize", Text(PageSize))),
            cancellationToken);

    public Task<CachedResult<ApprovalDetail>> GetRequestAsync(Guid id, CancellationToken cancellationToken = default) =>
        cache.GetAsync<ApprovalDetail>($"approvals/{id}", cancellationToken);

    /// <summary>
    /// The timeline of a document: <c>farmers</c>, <c>coops</c> or <c>contracts</c>.
    /// </summary>
    public Task<CachedResult<List<ApprovalDetail>>> GetTimelineAsync(
        string resource,
        Guid documentId,
        CancellationToken cancellationToken = default) =>
        cache.GetAsync<List<ApprovalDetail>>($"{resource}/{documentId}/approvals", cancellationToken);

    public Task<ApiResult> ApproveAsync(Guid id, string? note, CancellationToken cancellationToken = default) =>
        api.PostAsync($"approvals/{id}/approve", new { note = string.IsNullOrWhiteSpace(note) ? null : note.Trim() }, cancellationToken);

    public Task<ApiResult> RejectAsync(Guid id, string reason, CancellationToken cancellationToken = default) =>
        api.PostAsync($"approvals/{id}/reject", new { reason = reason.Trim() }, cancellationToken);

    public Task<ApiResult> CancelAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.PostAsync($"approvals/{id}/cancel", null, cancellationToken);

    public Task<CachedResult<List<ApprovalDelegation>>> GetDelegationsAsync(
        bool includeEnded,
        CancellationToken cancellationToken = default) =>
        cache.GetAsync<List<ApprovalDelegation>>(
            PartnershipApi.Path("approvals/delegations", ("includeEnded", includeEnded ? "true" : null)),
            cancellationToken);

    public Task<CachedResult<List<ApproverCandidate>>> GetDelegatesAsync(CancellationToken cancellationToken = default) =>
        cache.GetAsync<List<ApproverCandidate>>("approvals/delegates", cancellationToken);

    public Task<ApiResult<Guid>> DelegateAsync(
        Guid delegateUserId,
        DateOnly fromDate,
        DateOnly toDate,
        string? reason,
        CancellationToken cancellationToken = default) =>
        api.PostAsync<Guid>(
            "approvals/delegations",
            new { delegateUserId, fromDate, toDate, reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim() },
            Guid.NewGuid(),
            cancellationToken);

    public Task<ApiResult> RevokeDelegationAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.PostAsync($"approvals/delegations/{id}/revoke", null, cancellationToken);

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
}
