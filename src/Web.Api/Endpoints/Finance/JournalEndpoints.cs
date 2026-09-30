using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Finance.Journals;
using Domain.Finance.Journals;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed class JournalEndpoints : IEndpoint
{
    public sealed record UpdateRequest(DateOnly Date, string Description, IReadOnlyList<JournalLineRequest> Lines);

    public sealed record ReverseRequest(DateOnly Date, string Reason);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("finance/journals").WithTags(Tags.Journals);

        group.MapGet("", async (
            string? search,
            int? page,
            int? pageSize,
            Guid? branchId,
            JournalStatus? status,
            JournalSource? source,
            DateOnly? from,
            DateOnly? to,
            IQueryHandler<GetJournalsQuery, PagedList<JournalResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetJournalsQuery(new PageRequest(page, pageSize, search), branchId, status, source, from, to);

            Result<PagedList<JournalResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.JournalsRead);

        group.MapGet("{journalId:guid}", async (
            Guid journalId,
            IQueryHandler<GetJournalByIdQuery, JournalResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<JournalResponse> result = await handler.Handle(new GetJournalByIdQuery(journalId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.JournalsRead);

        group.MapPost("", async (
            CreateJournalCommand command,
            ICommandHandler<CreateJournalCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.JournalsCreate)
        .WithIdempotency();

        group.MapPut("{journalId:guid}", async (
            Guid journalId,
            UpdateRequest request,
            ICommandHandler<UpdateJournalCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateJournalCommand(journalId, request.Date, request.Description, request.Lines);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.JournalsCreate);

        group.MapDelete("{journalId:guid}", async (
            Guid journalId,
            ICommandHandler<DeleteJournalCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new DeleteJournalCommand(journalId), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.JournalsCreate);

        group.MapPost("{journalId:guid}/approve", async (
            Guid journalId,
            ICommandHandler<ApproveJournalCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new ApproveJournalCommand(journalId), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.JournalsApprove);

        group.MapPost("{journalId:guid}/post", async (
            Guid journalId,
            ICommandHandler<PostJournalCommand, string> handler,
            CancellationToken cancellationToken) =>
        {
            Result<string> result = await handler.Handle(new PostJournalCommand(journalId), cancellationToken);

            return result.Match(number => Results.Ok(new { number }), CustomResults.Problem);
        })
        .HasPermission(Permissions.JournalsPost);

        group.MapPost("{journalId:guid}/reverse", async (
            Guid journalId,
            ReverseRequest request,
            ICommandHandler<ReverseJournalCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(
                new ReverseJournalCommand(journalId, request.Date, request.Reason), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.JournalsPost);
    }
}
