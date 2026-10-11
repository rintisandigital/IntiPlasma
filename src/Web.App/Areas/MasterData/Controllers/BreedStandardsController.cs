using System.Globalization;
using System.Text;
using Application.Abstractions.Messaging;
using Application.BreedStandards;
using Domain.Access;
using Domain.MasterData.BreedStandards;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.MasterData.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;

namespace Web.App.Areas.MasterData.Controllers;

/// <summary>
/// Breed standards (PLAN-MOBILE M-69 s.d. M-73): the target curve per age of a broiler strain that the cycle
/// performance is compared with on the Performance tab and in the mobile app. Global for all branches; one active
/// standard is the default for cycles without a standard of their own. Rows are edited in the table or imported from CSV.
/// </summary>
[Area("MasterData")]
[MenuAccess(MenuCodes.MasterBreedStandards)]
public sealed class BreedStandardsController(
    PageSupport support,
    IQueryHandler<GetBreedStandardsQuery, IReadOnlyList<BreedStandardResponse>> standardsQuery,
    IQueryHandler<GetBreedStandardByIdQuery, BreedStandardDetailResponse> standardQuery) : AppController
{
    private const string MenuCode = MenuCodes.MasterBreedStandards;
    private const long MaxCsvBytes = 200_000;

    private static readonly ExportColumn<BreedStandardResponse>[] Columns =
    [
        new("Code", s => s.Code, Width: 1),
        new("Name", s => s.Name, Width: 2),
        new("Strain", s => s.Strain, Width: 1.2f),
        new("Ages", s => s.RowCount, ExportFormat.WholeNumber, 0.6f),
        new("Up to age", s => s.MaxAgeDays, ExportFormat.WholeNumber, 0.7f),
        new("Cycles", s => s.CycleCount, ExportFormat.WholeNumber, 0.6f),
        new("Default", s => s.IsDefault, ExportFormat.Boolean, 0.6f),
        new("Active", s => s.IsActive, ExportFormat.Boolean, 0.6f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken)
    {
        IReadOnlyList<BreedStandardResponse> standards = await ListAsync(search, cancellationToken);

        return View(new ListViewModel<BreedStandardResponse>
        {
            Rows = new PagedList<BreedStandardResponse>(standards, 1, Math.Max(standards.Count, 1), standards.Count),
            Search = search
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(string? format, string? search, CancellationToken cancellationToken) =>
        await support.ExportAsync(format, "Breed Standards", "breed-standards",
            string.IsNullOrWhiteSpace(search) ? [] : [$"Search: {search}"], Columns, await ListAsync(search, cancellationToken));

    /// <summary>
    /// The rows of one standard as CSV, in the import layout, to edit and import again.
    /// </summary>
    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> ExportRows(Guid id, CancellationToken cancellationToken)
    {
        Result<BreedStandardDetailResponse> standard = await standardQuery.Handle(new GetBreedStandardByIdQuery(id), cancellationToken);
        if (standard.IsFailure)
        {
            return NotFound();
        }

        byte[] csv = Encoding.UTF8.GetBytes(BreedStandardCsv.Write(standard.Value.Rows));

        return File(csv, "text/csv", $"breed-standard-{standard.Value.Code}.csv");
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public IActionResult Create() => View("Form", new BreedStandardFormViewModel { Rows = [new BreedStandardRowInput { AgeDays = 0 }] });

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        BreedStandardFormViewModel model,
        [FromServices] ICommandHandler<CreateBreedStandardCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<List<StandardPoint>> rows = string.IsNullOrWhiteSpace(model.RowsCsv)
                ? ToPoints(model.Rows)
                : BreedStandardCsv.Parse(model.RowsCsv);

            Result<Guid> result = rows.IsFailure
                ? Result.Failure<Guid>(rows.Error)
                : await handler.Handle(
                    new CreateBreedStandardCommand(model.Code, model.Name, model.Strain, model.Notes, rows.Value), cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Breed standard {model.Code} has been created.");
                return RedirectToAction(nameof(Edit), new { id = result.Value });
            }

            AddErrors(result.Error);
        }

        return View("Form", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        Result<BreedStandardDetailResponse> result = await standardQuery.Handle(new GetBreedStandardByIdQuery(id), cancellationToken);
        if (result.IsFailure)
        {
            return NotFound();
        }

        BreedStandardDetailResponse standard = result.Value;
        BreedStandardResponse? summary = (await ListAsync(null, cancellationToken)).FirstOrDefault(s => s.Id == id);

        return View("Form", new BreedStandardFormViewModel
        {
            Id = standard.Id,
            Code = standard.Code,
            Name = standard.Name,
            Strain = standard.Strain,
            Notes = standard.Notes,
            IsActive = standard.IsActive,
            IsDefault = standard.IsDefault,
            CycleCount = summary?.CycleCount ?? 0,
            Rows = [.. standard.Rows.Select(r => new BreedStandardRowInput
            {
                AgeDays = r.AgeDays,
                BodyWeightGram = r.BodyWeightGram,
                Fcr = r.Fcr,
                DepletionPercent = r.DepletionPercent,
                DailyFeedGram = r.DailyFeedGram,
                CumulativeFeedGram = r.CumulativeFeedGram
            })],
            CanSave = await support.CanAsync(MenuCode, MenuRights.Edit)
        });
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        BreedStandardFormViewModel model,
        [FromServices] ICommandHandler<UpdateBreedStandardCommand> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid && model.Id is Guid id)
        {
            Result<List<StandardPoint>> rows = ToPoints(model.Rows);

            Result result = rows.IsFailure
                ? Result.Failure(rows.Error)
                : await handler.Handle(
                    new UpdateBreedStandardCommand(id, model.Name, model.Strain, model.Notes, model.IsActive == true, rows.Value),
                    cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Breed standard {model.Code} has been saved.");
                return RedirectToAction(nameof(Edit), new { id });
            }

            AddErrors(result.Error);
        }

        BreedStandardResponse? summary = model.Id is Guid current
            ? (await ListAsync(null, cancellationToken)).FirstOrDefault(s => s.Id == current)
            : null;
        model.IsDefault = summary?.IsDefault ?? false;
        model.CycleCount = summary?.CycleCount ?? 0;

        return View("Form", model);
    }

    /// <summary>
    /// Replaces all rows from an uploaded or pasted CSV.
    /// </summary>
    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [RequestSizeLimit(MaxCsvBytes + 100_000)]
    public async Task<IActionResult> Import(
        Guid id,
        IFormFile? file,
        string? csv,
        [FromServices] ICommandHandler<ImportBreedStandardRowsCommand, int> handler,
        CancellationToken cancellationToken)
    {
        string content = csv ?? string.Empty;
        if (file is { Length: > 0 })
        {
            if (file.Length > MaxCsvBytes)
            {
                NotifyError("The CSV file is too large (max 200 KB).");
                return RedirectToAction(nameof(Edit), new { id });
            }

            using var reader = new StreamReader(file.OpenReadStream());
            content = await reader.ReadToEndAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            NotifyError("Choose a CSV file or paste the rows.");
            return RedirectToAction(nameof(Edit), new { id });
        }

        Result<int> result = await handler.Handle(new ImportBreedStandardRowsCommand(id, content), cancellationToken);

        if (result.IsSuccess)
        {
            NotifySuccess($"{result.Value.ToString(CultureInfo.InvariantCulture)} row(s) imported; the previous rows were replaced.");
        }
        else
        {
            NotifyError(result.Error.Description);
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> SetDefault(
        Guid id,
        [FromServices] ICommandHandler<SetDefaultBreedStandardCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new SetDefaultBreedStandardCommand(id), cancellationToken);

        if (result.IsSuccess)
        {
            NotifySuccess("The breed standard is now the default for cycles without a standard of their own.");
        }
        else
        {
            NotifyError(result.Error.Description);
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    private static Result<List<StandardPoint>> ToPoints(IEnumerable<BreedStandardRowInput> rows)
    {
        var points = new List<StandardPoint>();

        foreach (BreedStandardRowInput row in rows.Where(r => r.AgeDays is not null || r.BodyWeightGram is not null))
        {
            if (row.AgeDays is not int age || row.BodyWeightGram is not decimal bodyWeight)
            {
                return Result.Failure<List<StandardPoint>>(Error.Problem(
                    "BreedStandards.IncompleteRow", "Every row needs an age and a body weight"));
            }

            points.Add(new StandardPoint(age, bodyWeight, row.Fcr, row.DepletionPercent, row.DailyFeedGram, row.CumulativeFeedGram));
        }

        return points;
    }

    private async Task<IReadOnlyList<BreedStandardResponse>> ListAsync(string? search, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<BreedStandardResponse>> result = await standardsQuery.Handle(new GetBreedStandardsQuery(), cancellationToken);
        IReadOnlyList<BreedStandardResponse> standards = result.IsSuccess ? result.Value : [];

        return string.IsNullOrWhiteSpace(search)
            ? standards
            : [.. standards.Where(s => s.Code.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                      s.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                      (s.Strain?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))];
    }
}
