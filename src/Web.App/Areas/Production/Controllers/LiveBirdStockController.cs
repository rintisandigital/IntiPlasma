using Application.Abstractions.Messaging;
using Application.Production;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Production.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Areas.Production.Controllers;

/// <summary>
/// Rekap stok ayam harian for Sales (PLAN-MOBILE M-28, M-51): per running cycle the latest report of the PPL on or
/// before the chosen date, totalled per weight range, with the age of the data. Read-only: the PPL enter the stock on
/// the mobile app.
/// </summary>
[Area("Production")]
[MenuAccess(MenuCodes.ProductionLiveBirdStock)]
public sealed class LiveBirdStockController(
    PageSupport support,
    DisplayFormatter formatter,
    IQueryHandler<GetLiveBirdStockSummaryQuery, LiveBirdStockSummaryResponse> summaryQuery) : AppController
{
    private static readonly ExportColumn<LiveBirdStockExportRow>[] Columns =
    [
        new("Branch", r => r.Branch, Width: 0.8f),
        new("Coop", r => r.Coop, Width: 2),
        new("Cycle", r => r.Cycle, Width: 1.5f),
        new("Age (days)", r => r.AgeDays, ExportFormat.WholeNumber, 0.7f),
        new("Population", r => r.Population, ExportFormat.WholeNumber, 1),
        new("Report date", r => r.ReportDate, ExportFormat.Date, 1),
        new("Data age (days)", r => r.DataAgeDays, ExportFormat.WholeNumber, 0.8f),
        new("Weight range", r => r.WeightRange, Width: 1),
        new("Birds", r => r.Birds, ExportFormat.WholeNumber, 1),
        new("Weight (kg)", r => r.WeightKg, ExportFormat.Number, 1.2f),
        new("Average (kg)", r => r.AverageWeightKg, ExportFormat.Number, 1)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? date, string? branch, CancellationToken cancellationToken)
    {
        DateOnly day = date ?? formatter.Today();
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<LiveBirdStockSummaryResponse> summary = await summaryQuery.Handle(
            new GetLiveBirdStockSummaryQuery(day, branchFilter.BranchId), cancellationToken);

        return View(new LiveBirdStockViewModel
        {
            Date = day,
            BranchOptions = branchFilter.Options,
            Summary = summary.IsSuccess ? summary.Value : null,
            Error = summary.IsSuccess ? null : summary.Error.Description
        });
    }

    [HttpGet]
    [MenuAccess(MenuCodes.ProductionLiveBirdStock, MenuRights.Export)]
    public async Task<IActionResult> Export(string? format, DateOnly? date, string? branch, CancellationToken cancellationToken)
    {
        DateOnly day = date ?? formatter.Today();
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<LiveBirdStockSummaryResponse> summary = await summaryQuery.Handle(
            new GetLiveBirdStockSummaryQuery(day, branchFilter.BranchId), cancellationToken);

        IReadOnlyList<LiveBirdStockExportRow> rows = summary.IsSuccess ? LiveBirdStockExportRow.From(summary.Value) : [];

        return await support.ExportAsync(format, "Live Bird Stock", $"live-bird-stock-{day:yyyyMMdd}",
            [$"Date: {formatter.Date(day)}", branchFilter.Description], Columns, rows);
    }
}
