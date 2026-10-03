using Application.Abstractions.Messaging;
using Application.Finance.CostCenters;
using Application.Finance.JournalTemplates;
using Domain.Access;
using Domain.Finance.Accounts;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Finance.Models;
using Web.App.Areas.MasterData.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;

namespace Web.App.Areas.Finance.Controllers;

/// <summary>
/// Reusable manual journal layouts (accounts + side, no amounts) that pre-fill a new journal.
/// </summary>
[Area("Finance")]
[MenuAccess(MenuCodes.FinanceJournalTemplates)]
public sealed class JournalTemplatesController(
    PageSupport support,
    IQueryHandler<GetJournalTemplatesQuery, IReadOnlyList<JournalTemplateResponse>> templatesQuery,
    IQueryHandler<GetCostCentersQuery, IReadOnlyList<CostCenterResponse>> costCentersQuery) : AppController
{
    private const string MenuCode = MenuCodes.FinanceJournalTemplates;

    private static readonly ExportColumn<JournalTemplateRow>[] Columns =
    [
        new("Template", r => r.Template, Width: 2),
        new("Line", r => r.Line, ExportFormat.WholeNumber, 0.5f),
        new("Account", r => r.Account, Width: 3),
        new("Side", r => r.Side, Width: 0.7f),
        new("Line description", r => r.Description, Width: 2.5f),
        new("Active", r => r.IsActive, ExportFormat.Boolean, 0.7f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken)
    {
        IReadOnlyList<JournalTemplateResponse> templates = await ListAsync(search, cancellationToken);

        return View(new ListViewModel<JournalTemplateResponse>
        {
            Rows = new PagedList<JournalTemplateResponse>(templates, 1, Math.Max(templates.Count, 1), templates.Count),
            Search = search
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(string? format, string? search, CancellationToken cancellationToken)
    {
        JournalTemplateRow[] rows =
        [
            .. (await ListAsync(search, cancellationToken)).SelectMany(t => t.Lines.Select(l => new JournalTemplateRow(
                t.Name, l.LineNumber, $"{l.AccountCode} — {l.AccountName}", l.Side, l.Description, t.IsActive)))
        ];

        return await support.ExportAsync(format, "Journal Templates", "journal-templates",
            string.IsNullOrWhiteSpace(search) ? [] : [$"Search: {search}"], Columns, rows);
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken) =>
        View("Form", await WithOptionsAsync(
            new JournalTemplateFormViewModel
            {
                Lines = [new TemplateLineInput { Side = BalanceSide.Debit }, new TemplateLineInput { Side = BalanceSide.Credit }]
            },
            cancellationToken));

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        JournalTemplateFormViewModel model,
        [FromServices] ICommandHandler<CreateJournalTemplateCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(
                new CreateJournalTemplateCommand(model.Name, model.Description, Lines(model)), cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Journal template \"{model.Name}\" has been created.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        JournalTemplateResponse? template = (await ListAsync(null, cancellationToken)).FirstOrDefault(t => t.Id == id);

        if (template is null)
        {
            return NotFound();
        }

        var model = new JournalTemplateFormViewModel
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            IsActive = template.IsActive,
            Lines =
            [
                .. template.Lines.OrderBy(l => l.LineNumber).Select(l => new TemplateLineInput
                {
                    AccountId = l.AccountId,
                    AccountLabel = $"{l.AccountCode} — {l.AccountName}",
                    CostCenterId = l.CostCenterId,
                    Side = Enum.Parse<BalanceSide>(l.Side),
                    Description = l.Description
                })
            ],
            CanSave = await support.CanAsync(MenuCode, MenuRights.Edit)
        };

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        JournalTemplateFormViewModel model,
        [FromServices] ICommandHandler<UpdateJournalTemplateCommand> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid && model.Id is Guid id)
        {
            Result result = await handler.Handle(
                new UpdateJournalTemplateCommand(id, model.Name, model.Description, model.IsActive == true, Lines(model)),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Journal template \"{model.Name}\" has been saved.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    private static JournalTemplateLineRequest[] Lines(JournalTemplateFormViewModel model) =>
        [.. model.Lines.Select(l => new JournalTemplateLineRequest(l.AccountId!.Value, l.CostCenterId, l.Side!.Value, l.Description))];

    private async Task<IReadOnlyList<JournalTemplateResponse>> ListAsync(string? search, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<JournalTemplateResponse>> result = await templatesQuery.Handle(new GetJournalTemplatesQuery(), cancellationToken);

        if (result.IsFailure)
        {
            return [];
        }

        return string.IsNullOrWhiteSpace(search)
            ? result.Value
            : [.. result.Value.Where(t => t.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))];
    }

    private async Task<JournalTemplateFormViewModel> WithOptionsAsync(JournalTemplateFormViewModel model, CancellationToken cancellationToken)
    {
        model.CostCenters = await FinanceOptions.CostCentersAsync(costCentersQuery, model.Lines.Select(l => l.CostCenterId), cancellationToken);
        return model;
    }

    private sealed record JournalTemplateRow(string Template, int Line, string Account, string Side, string? Description, bool IsActive);
}
