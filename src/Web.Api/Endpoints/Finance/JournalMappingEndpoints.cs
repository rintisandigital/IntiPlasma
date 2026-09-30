using Application.Abstractions.Messaging;
using Application.Finance.JournalMappings;
using Domain.Finance.JournalMappings;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

/// <summary>
/// Configuration of the auto journal engine.
/// </summary>
internal sealed class JournalMappingEndpoints : IEndpoint
{
    public sealed record UpdateRequest(string? Description, bool IsActive, IReadOnlyList<JournalMappingLineRequest> Lines);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("finance/journal-mappings").WithTags(Tags.JournalMappings);

        group.MapGet("events", () => Results.Ok(AccountingEvents.All))
            .HasPermission(Permissions.FinanceSetupRead);

        group.MapGet("", async (
            string? eventType,
            IQueryHandler<GetJournalMappingsQuery, IReadOnlyList<JournalMappingResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<JournalMappingResponse>> result = await handler.Handle(
                new GetJournalMappingsQuery(eventType), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.FinanceSetupRead);

        group.MapPost("", async (
            CreateJournalMappingCommand command,
            ICommandHandler<CreateJournalMappingCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.FinanceSetupManage)
        .WithIdempotency();

        group.MapPut("{journalMappingId:guid}", async (
            Guid journalMappingId,
            UpdateRequest request,
            ICommandHandler<UpdateJournalMappingCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateJournalMappingCommand(journalMappingId, request.Description, request.IsActive, request.Lines);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.FinanceSetupManage);

        // Dry run: POST because the request carries a list of amounts; nothing is saved.
        group.MapPost("preview", async (
            PreviewAutoJournalQuery query,
            IQueryHandler<PreviewAutoJournalQuery, IReadOnlyList<PreviewJournalLine>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<PreviewJournalLine>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.FinanceSetupRead);
    }
}
