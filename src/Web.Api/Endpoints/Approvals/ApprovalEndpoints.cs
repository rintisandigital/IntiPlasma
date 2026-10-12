using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Approvals;
using Domain.Approvals;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Approvals;

/// <summary>
/// Multi-level approval (PLAN-MOBILE §5, M-90). Deciding needs <c>approvals:decide</c>; "my submissions", the detail of
/// one's own request and cancelling it only need a signed-in user. Flows are configured in the WebApp only.
/// </summary>
internal sealed class ApprovalEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("approvals").WithTags(Tags.Approvals);

        group.MapGet("inbox", async (
            string? search,
            int? page,
            int? pageSize,
            ApprovalDocumentType? documentType,
            IQueryHandler<GetApprovalInboxQuery, PagedList<ApprovalRequestResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<PagedList<ApprovalRequestResponse>> result = await handler.Handle(
                new GetApprovalInboxQuery(new PageRequest(page, pageSize, search), documentType), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.ApprovalsDecide);

        group.MapGet("inbox/count", async (
            IQueryHandler<GetApprovalInboxCountQuery, ApprovalInboxCountResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<ApprovalInboxCountResponse> result = await handler.Handle(new GetApprovalInboxCountQuery(), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.ApprovalsDecide);

        group.MapGet("mine", async (
            string? search,
            int? page,
            int? pageSize,
            ApprovalStatus? status,
            IQueryHandler<GetMySubmissionsQuery, PagedList<ApprovalRequestResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<PagedList<ApprovalRequestResponse>> result = await handler.Handle(
                new GetMySubmissionsQuery(new PageRequest(page, pageSize, search), status), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization();

        group.MapGet("{requestId:guid}", async (
            Guid requestId,
            IQueryHandler<GetApprovalRequestQuery, ApprovalDetailResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<ApprovalDetailResponse> result = await handler.Handle(new GetApprovalRequestQuery(requestId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization();

        group.MapPost("{requestId:guid}/approve", async (
            Guid requestId,
            ApproveRequest? request,
            ICommandHandler<ApproveRequestCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new ApproveRequestCommand(requestId, request?.Note), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.ApprovalsDecide);

        group.MapPost("{requestId:guid}/reject", async (
            Guid requestId,
            RejectRequest request,
            ICommandHandler<RejectRequestCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new RejectRequestCommand(requestId, request.Reason), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.ApprovalsDecide);

        group.MapPost("{requestId:guid}/cancel", async (
            Guid requestId,
            ICommandHandler<CancelRequestCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new CancelRequestCommand(requestId), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .RequireAuthorization();

        // Delegation while on leave (M-98): always for the signed-in approver.
        group.MapGet("delegations", async (
            bool? includeEnded,
            IQueryHandler<GetApprovalDelegationsQuery, IReadOnlyList<ApprovalDelegationResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<ApprovalDelegationResponse>> result = await handler.Handle(
                new GetApprovalDelegationsQuery(AllUsers: false, IncludeEnded: includeEnded ?? false), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.ApprovalsDecide);

        group.MapGet("delegates", async (
            IQueryHandler<GetApproverCandidatesQuery, IReadOnlyList<ApproverCandidateResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<ApproverCandidateResponse>> result =
                await handler.Handle(new GetApproverCandidatesQuery(), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.ApprovalsDecide);

        group.MapPost("delegations", async (
            DelegationRequest request,
            ICommandHandler<CreateApprovalDelegationCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(
                new CreateApprovalDelegationCommand(request.DelegateUserId, request.FromDate, request.ToDate, request.Reason),
                cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.ApprovalsDecide)
        .WithIdempotency();

        group.MapPost("delegations/{delegationId:guid}/revoke", async (
            Guid delegationId,
            ICommandHandler<RevokeApprovalDelegationCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new RevokeApprovalDelegationCommand(delegationId), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.ApprovalsDecide);
    }

    internal sealed record ApproveRequest(string? Note);

    internal sealed record RejectRequest(string Reason);

    internal sealed record DelegationRequest(Guid DelegateUserId, DateOnly FromDate, DateOnly ToDate, string? Reason);
}
