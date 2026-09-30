using Application.Abstractions.Messaging;
using Application.Production;
using Domain.Partnership.Cycles;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Production;

internal sealed class ProductionEndpoints : IEndpoint
{
    public sealed record ReviseRequest(
        string Reason,
        int Mortality,
        int Culling,
        decimal? AverageBodyWeightGram,
        string? Notes,
        IReadOnlyList<UsageRequest> Usages);

    public sealed record HarvestRequest(DateOnly Date, int Birds, decimal WeightKg, string? Notes);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        MapDailyRecordings(app.MapGroup("production/daily-recordings").WithTags(Tags.DailyRecordings));
        MapCycleProduction(app.MapGroup("cycles").WithTags(Tags.Cycles));
    }

    private static void MapDailyRecordings(RouteGroupBuilder group)
    {
        group.MapGet("", async (
            Guid cycleId,
            DateOnly? from,
            DateOnly? to,
            IQueryHandler<GetDailyRecordingsQuery, IReadOnlyList<DailyRecordingResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<DailyRecordingResponse>> result = await handler.Handle(
                new GetDailyRecordingsQuery(cycleId, from, to), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.ProductionRead);

        group.MapGet("{dailyRecordingId:guid}", async (
            Guid dailyRecordingId,
            IQueryHandler<GetDailyRecordingByIdQuery, DailyRecordingResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<DailyRecordingResponse> result = await handler.Handle(
                new GetDailyRecordingByIdQuery(dailyRecordingId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.ProductionRead);

        group.MapPost("", async (
            CreateDailyRecordingCommand command,
            ICommandHandler<CreateDailyRecordingCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.ProductionRecord)
        .WithIdempotency();

        group.MapPut("{dailyRecordingId:guid}", async (
            Guid dailyRecordingId,
            ReviseRequest request,
            ICommandHandler<ReviseDailyRecordingCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new ReviseDailyRecordingCommand(
                dailyRecordingId,
                request.Reason,
                request.Mortality,
                request.Culling,
                request.AverageBodyWeightGram,
                request.Notes,
                request.Usages);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.ProductionRevise);
    }

    private static void MapCycleProduction(RouteGroupBuilder group)
    {
        group.MapGet("{cycleId:guid}/performance", async (
            Guid cycleId,
            IQueryHandler<GetCyclePerformanceQuery, CyclePerformanceResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<CyclePerformanceResponse> result = await handler.Handle(new GetCyclePerformanceQuery(cycleId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.ProductionRead);

        group.MapPost("{cycleId:guid}/harvests", async (
            Guid cycleId,
            HarvestRequest request,
            ICommandHandler<RecordHarvestCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new RecordHarvestCommand(cycleId, request.Date, request.Birds, request.WeightKg, request.Notes);

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.ProductionRecord)
        .WithIdempotency();

        group.MapPost("{cycleId:guid}/close", async (
            Guid cycleId,
            ICommandHandler<CloseCycleCommand, CyclePerformance> handler,
            CancellationToken cancellationToken) =>
        {
            Result<CyclePerformance> result = await handler.Handle(new CloseCycleCommand(cycleId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.ProductionClose);
    }
}
