using System.Globalization;
using Application.Abstractions.Messaging;
using Application.Finance.FiscalPeriods;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Finance.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;
using Web.App.Infrastructure.Workflow;

namespace Web.App.Areas.Finance.Controllers;

/// <summary>
/// Monthly fiscal periods: open a year (12 periods), check the closing checklist, close in order (closing December
/// also posts the year-end closing journal) and reopen from the latest closed period backwards.
/// </summary>
[Area("Finance")]
[MenuAccess(MenuCodes.FinanceFiscalPeriods)]
public sealed class FiscalPeriodsController(
    PageSupport support,
    DisplayFormatter formatter,
    IQueryHandler<GetFiscalPeriodsQuery, IReadOnlyList<FiscalPeriodResponse>> periodsQuery) : AppController
{
    private const string MenuCode = MenuCodes.FinanceFiscalPeriods;
    private const string DocumentType = "FiscalPeriod";

    private static readonly ExportColumn<FiscalPeriodResponse>[] Columns =
    [
        new("Period", p => PeriodName(p.Year, p.Month), Width: 1.5f),
        new("Start", p => p.StartDate, ExportFormat.Date, 1),
        new("End", p => p.EndDate, ExportFormat.Date, 1),
        new("Status", p => p.Status, Width: 1),
        new("Closed at", p => p.ClosedAtUtc, ExportFormat.DateTime, 1.5f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(int? year, CancellationToken cancellationToken)
    {
        IReadOnlyList<FiscalPeriodResponse> all = await ListAsync(null, cancellationToken);
        int[] years = [.. all.Select(p => p.Year).Distinct().OrderDescending()];
        int currentYear = formatter.Today().Year;
        int? selected = year ?? (years.Contains(currentYear) ? currentYear : years.Cast<int?>().FirstOrDefault());

        return View(new FiscalPeriodListViewModel
        {
            Periods = [.. all.Where(p => p.Year == selected)],
            Year = selected,
            Years = years,
            CanOpenYear = await support.CanAsync(MenuCode, MenuRights.Create),
            CanChangeStatus = await support.CanAsync(MenuCode, MenuRights.Edit),
            SuggestedYear = years.Length == 0 ? currentYear : years.Max() + 1
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(string? format, int? year, CancellationToken cancellationToken) =>
        await support.ExportAsync(format, "Fiscal Periods", year is null ? "fiscal-periods" : $"fiscal-periods-{year}",
            year is null ? [] : [$"Year: {year}"], Columns, await ListAsync(year, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid id,
        [FromServices] IQueryHandler<GetPeriodClosingChecklistQuery, PeriodClosingChecklist> query,
        CancellationToken cancellationToken)
    {
        Result<PeriodClosingChecklist> result = await query.Handle(new GetPeriodClosingChecklistQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return NotFound();
        }

        return View(new FiscalPeriodChecklistViewModel(result.Value, await support.CanAsync(MenuCode, MenuRights.Edit)));
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> OpenYear(
        int? year,
        [FromServices] ICommandHandler<OpenFiscalYearCommand, int> handler,
        CancellationToken cancellationToken)
    {
        if (year is null)
        {
            NotifyError("Enter the fiscal year to open.");
            return RedirectToAction(nameof(Index));
        }

        Result<int> result = await handler.Handle(new OpenFiscalYearCommand(year.Value), cancellationToken);

        if (result.IsSuccess)
        {
            NotifySuccess($"Fiscal year {year} has been opened ({result.Value} periods).");
        }
        else
        {
            NotifyError(result.Error.Description);
        }

        return RedirectToAction(nameof(Index), new { year });
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("close")]
    public async Task<IActionResult> Close(
        Guid id,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<CloseFiscalPeriodCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "close"),
            ct => handler.Handle(new CloseFiscalPeriodCommand(id), ct),
            cancellationToken);

        return AfterAction(id, result, "The period has been closed; postings dated in it are now refused.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("reopen")]
    public async Task<IActionResult> Reopen(
        Guid id,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<ReopenFiscalPeriodCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "reopen"),
            ct => handler.Handle(new ReopenFiscalPeriodCommand(id), ct),
            cancellationToken);

        return AfterAction(id, result, "The period has been reopened.");
    }

    public static string PeriodName(int year, int month) =>
        new DateOnly(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    private RedirectToActionResult AfterAction(Guid id, Result result, string successMessage)
    {
        if (result.IsSuccess)
        {
            NotifySuccess(successMessage);
        }
        else
        {
            NotifyError(result.Error.Description);
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<IReadOnlyList<FiscalPeriodResponse>> ListAsync(int? year, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<FiscalPeriodResponse>> result = await periodsQuery.Handle(new GetFiscalPeriodsQuery(year), cancellationToken);
        return result.IsSuccess ? result.Value : [];
    }
}
