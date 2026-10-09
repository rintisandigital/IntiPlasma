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
        IReadOnlyList<UsageRequest> Usages,
        IReadOnlyList<Guid>? Documents = null);

    public sealed record HarvestRequest(DateOnly Date, int Birds, decimal WeightKg, string? Notes, IReadOnlyList<Guid>? Documents = null);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        MapDailyRecordings(app.MapGroup("production/daily-recordings").WithTags(Tags.DailyRecordings));
        MapCycleProduction(app.MapGroup("cycles").WithTags(Tags.Cycles));
        MapLiveBirdStock(app.MapGroup("production/live-bird-stocks").WithTags(Tags.LiveBirdStock));
    }

    /// <summary>
    /// Stok ayam harian (PLAN-MOBILE §4.4): entries per weight range, upserted by the PPL (offline queue), and the
    /// branch summary for the Manager and Sales.
    /// </summary>
    private static void MapLiveBirdStock(RouteGroupBuilder group)
    {
        group.MapGet("", async (
            Guid? cycleId,
            Guid? branchId,
            DateOnly? date,
            DateOnly? from,
            DateOnly? to,
            IQueryHandler<GetLiveBirdStockEntriesQuery, IReadOnlyList<LiveBirdStockEntryResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<LiveBirdStockEntryResponse>> result = await handler.Handle(
                new GetLiveBirdStockEntriesQuery(cycleId, branchId, date, from, to), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.ProductionRead);

        group.MapGet("summary", async (
            DateOnly date,
            Guid? branchId,
            IQueryHandler<GetLiveBirdStockSummaryQuery, LiveBirdStockSummaryResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<LiveBirdStockSummaryResponse> result = await handler.Handle(
                new GetLiveBirdStockSummaryQuery(date, branchId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.ProductionRead);

        group.MapPost("", async (
            UpsertLiveBirdStockEntryCommand command,
            ICommandHandler<UpsertLiveBirdStockEntryCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.ProductionStockReport)
        .WithIdempotency();

        group.MapDelete("{entryId:guid}", async (
            Guid entryId,
            ICommandHandler<DeleteLiveBirdStockEntryCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new DeleteLiveBirdStockEntryCommand(entryId), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.ProductionStockReport);
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
                request.Usages,
                request.Documents);

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
            var command = new RecordHarvestCommand(cycleId, request.Date, request.Birds, request.WeightKg, request.Notes, request.Documents);

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
