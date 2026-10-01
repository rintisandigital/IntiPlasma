using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Cycles;
using Application.Cycles.Cancel;
using Application.Cycles.Get;
using Application.Cycles.GetById;
using Application.Cycles.Plan;
using Application.Cycles.Start;
using Domain.Partnership.Cycles;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Partnership;

internal sealed class CycleEndpoints : IEndpoint
{
    public sealed record StartRequest(DateOnly ChickInDate, IReadOnlyList<ChickInLine> Lines, IReadOnlyList<Guid>? Documents = null);

    public sealed record CancelRequest(string Reason);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("cycles").WithTags(Tags.Cycles);

        group.MapGet("", async (
            string? search,
            int? page,
            int? pageSize,
            Guid? branchId,
            Guid? farmerId,
            Guid? coopId,
            CycleStatus? status,
            IQueryHandler<GetCyclesQuery, PagedList<CycleResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetCyclesQuery(new PageRequest(page, pageSize, search), branchId, farmerId, coopId, status);

            Result<PagedList<CycleResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.CyclesRead);

        group.MapGet("{cycleId:guid}", async (
            Guid cycleId,
            IQueryHandler<GetCycleByIdQuery, CycleResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<CycleResponse> result = await handler.Handle(new GetCycleByIdQuery(cycleId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.CyclesRead);

        group.MapPost("", async (
            PlanCycleCommand command,
            ICommandHandler<PlanCycleCommand, PlanCycleResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<PlanCycleResponse> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.CyclesManage)
        .WithIdempotency();

        group.MapPost("{cycleId:guid}/start", async (
            Guid cycleId,
            StartRequest request,
            ICommandHandler<StartCycleCommand, int> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new StartCycleCommand(cycleId, request.ChickInDate, request.Lines, request.Documents);

            Result<int> result = await handler.Handle(command, cancellationToken);

            return result.Match(population => Results.Ok(new { initialPopulation = population }), CustomResults.Problem);
        })
        .HasPermission(Permissions.ProductionRecord);

        group.MapPost("{cycleId:guid}/cancel", async (
            Guid cycleId,
            CancelRequest request,
            ICommandHandler<CancelCycleCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new CancelCycleCommand(cycleId, request.Reason), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.CyclesManage);
    }
}
