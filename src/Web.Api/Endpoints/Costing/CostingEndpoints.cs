using Application.Abstractions.Messaging;
using Application.Costing;
using Domain.Costing.PlasmaSettlements;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Costing;

internal sealed class CostingEndpoints : IEndpoint
{
    public sealed record RecalculateRequest(DateOnly SettlementDate, decimal DebtDeduction, string? Notes);

    public sealed record ReasonRequest(string Reason);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("cycles/{cycleId:guid}/cost", async (
            Guid cycleId,
            IQueryHandler<GetCycleCostQuery, CycleCostResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<CycleCostResponse> result = await handler.Handle(new GetCycleCostQuery(cycleId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.CycleCost)
        .HasPermission(Permissions.CostingRead);

        MapSettlements(app.MapGroup("costing/settlements").WithTags(Tags.PlasmaSettlements));
    }

    private static void MapSettlements(RouteGroupBuilder group)
    {
        group.MapGet("", async (
            Guid? branchId,
            Guid? farmerId,
            Guid? cycleId,
            PlasmaSettlementStatus? status,
            IQueryHandler<GetPlasmaSettlementsQuery, IReadOnlyList<PlasmaSettlementResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<PlasmaSettlementResponse>> result = await handler.Handle(
                new GetPlasmaSettlementsQuery(branchId, farmerId, cycleId, status), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.CostingRead);

        group.MapGet("{plasmaSettlementId:guid}", async (
            Guid plasmaSettlementId,
            IQueryHandler<GetPlasmaSettlementByIdQuery, PlasmaSettlementResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<PlasmaSettlementResponse> result = await handler.Handle(
                new GetPlasmaSettlementByIdQuery(plasmaSettlementId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.CostingRead);

        group.MapPost("", async (
            CreatePlasmaSettlementCommand command,
            ICommandHandler<CreatePlasmaSettlementCommand, CreatePlasmaSettlementResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<CreatePlasmaSettlementResponse> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.SettlementsManage)
        .WithIdempotency();

        group.MapPut("{plasmaSettlementId:guid}", async (
            Guid plasmaSettlementId,
            RecalculateRequest request,
            ICommandHandler<RecalculatePlasmaSettlementCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new RecalculatePlasmaSettlementCommand(plasmaSettlementId, request.SettlementDate, request.DebtDeduction, request.Notes),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.SettlementsManage);

        group.MapPost("{plasmaSettlementId:guid}/approve", async (
            Guid plasmaSettlementId,
            ICommandHandler<ApprovePlasmaSettlementCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new ApprovePlasmaSettlementCommand(plasmaSettlementId), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.SettlementsApprove);

        group.MapPost("{plasmaSettlementId:guid}/cancel", async (
            Guid plasmaSettlementId,
            ReasonRequest request,
            ICommandHandler<CancelPlasmaSettlementCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new CancelPlasmaSettlementCommand(plasmaSettlementId, request.Reason), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.SettlementsManage);
    }
}
