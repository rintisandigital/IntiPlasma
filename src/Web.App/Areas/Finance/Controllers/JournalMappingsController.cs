using Application.Abstractions.Messaging;
using Application.Finance.Accounts;
using Application.Finance.CostCenters;
using Application.Finance.JournalMappings;
using Domain.Access;
using Domain.Finance.JournalMappings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;
using Web.App.Areas.Finance.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;

namespace Web.App.Areas.Finance.Controllers;

/// <summary>
/// Auto journal mappings: for every accounting event the operational modules publish, each amount component is
/// mapped to a debit and a credit account. A company-wide default applies to every branch unless the branch has
/// its own mapping.
/// </summary>
[Area("Finance")]
[MenuAccess(MenuCodes.FinanceJournalMappings)]
public sealed class JournalMappingsController(
    PageSupport support,
    IQueryHandler<GetJournalMappingsQuery, IReadOnlyList<JournalMappingResponse>> mappingsQuery,
    IQueryHandler<GetAccountsQuery, IReadOnlyList<AccountResponse>> accountsQuery,
    IQueryHandler<GetCostCentersQuery, IReadOnlyList<CostCenterResponse>> costCentersQuery) : AppController
{
    private const string MenuCode = MenuCodes.FinanceJournalMappings;

    private static readonly ExportColumn<MappingRow>[] Columns =
    [
        new("Event", r => r.Event, Width: 2.5f),
        new("Branch", r => r.Branch, Width: 1),
        new("Component", r => r.Component, Width: 2.5f),
        new("Debit", r => r.Debit, Width: 1),
        new("Credit", r => r.Credit, Width: 1),
        new("Active", r => r.IsActive, ExportFormat.Boolean, 0.7f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        IReadOnlyList<JournalMappingResponse> mappings = await ListAsync(cancellationToken);

        return View(AccountingEvents.All
            .Select(e => new JournalMappingEventRow(
                e,
                mappings.FirstOrDefault(m => m.EventType == e.Code && m.BranchId is null),
                [.. mappings.Where(m => m.EventType == e.Code && m.BranchId is not null)]))
            .ToList());
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(string? format, CancellationToken cancellationToken)
    {
        MappingRow[] rows =
        [
            .. (await ListAsync(cancellationToken)).SelectMany(m =>
            {
                AccountingEventDefinition? definition = AccountingEvents.Find(m.EventType);
                return m.Lines.Select(l => new MappingRow(
                    AccountingEventLabels.Event(m.EventType),
                    m.BranchCode ?? "Default",
                    definition?.Components.FirstOrDefault(c => c.Code == l.Component) is { } component
                        ? AccountingEventLabels.Component(m.EventType, component)
                        : l.Component,
                    l.DebitAccountCode,
                    l.CreditAccountCode,
                    m.IsActive));
            })
        ];

        return await support.ExportAsync(format, "Auto Journal Mappings", "journal-mappings", [], Columns, rows);
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(string? eventType, bool? fromDefault, CancellationToken cancellationToken)
    {
        AccountingEventDefinition definition = AccountingEvents.Find(eventType ?? string.Empty) ?? AccountingEvents.All[0];

        var model = new JournalMappingFormViewModel
        {
            EventType = definition.Code,
            Lines = [.. definition.Components.Select(c => new MappingLineInput { Component = c.Code })]
        };

        // A branch override starts from the company default so only the differences need to be changed.
        if (fromDefault == true &&
            (await ListAsync(cancellationToken)).FirstOrDefault(m => m.EventType == definition.Code && m.BranchId is null) is { } fallback)
        {
            model.Lines = [.. definition.Components.Select(c => ToInput(c.Code, fallback.Lines.FirstOrDefault(l => l.Component == c.Code)))];
        }

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        JournalMappingFormViewModel model,
        [FromServices] ICommandHandler<CreateJournalMappingCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        JournalMappingLineRequest[] lines = Lines(model);

        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(
                new CreateJournalMappingCommand(model.EventType, model.BranchId, model.Description, lines), cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"The mapping for \"{AccountingEventLabels.Event(model.EventType)}\" has been created.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        JournalMappingResponse? mapping = (await ListAsync(cancellationToken)).FirstOrDefault(m => m.Id == id);

        if (mapping is null)
        {
            return NotFound();
        }

        IEnumerable<string> components = AccountingEvents.Find(mapping.EventType)?.Components.Select(c => c.Code)
            ?? mapping.Lines.Select(l => l.Component);

        var model = new JournalMappingFormViewModel
        {
            Id = mapping.Id,
            EventType = mapping.EventType,
            BranchId = mapping.BranchId,
            BranchCode = mapping.BranchCode,
            Description = mapping.Description,
            IsActive = mapping.IsActive,
            Lines = [.. components.Select(c => ToInput(c, mapping.Lines.FirstOrDefault(l => l.Component == c)))],
            CanSave = await support.CanAsync(MenuCode, MenuRights.Edit)
        };

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        JournalMappingFormViewModel model,
        [FromServices] ICommandHandler<UpdateJournalMappingCommand> handler,
        CancellationToken cancellationToken)
    {
        JournalMappingLineRequest[] lines = Lines(model);

        if (ModelState.IsValid && model.Id is Guid id)
        {
            Result result = await handler.Handle(
                new UpdateJournalMappingCommand(id, model.Description, model.IsActive == true, lines), cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"The mapping for \"{AccountingEventLabels.Event(model.EventType)}\" has been saved.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    private static MappingLineInput ToInput(string component, JournalMappingLineResponse? line) => new()
    {
        Component = component,
        DebitAccountId = line?.DebitAccountId,
        CreditAccountId = line?.CreditAccountId,
        CostCenterId = line?.CostCenterId
    };

    /// <summary>
    /// Mapped components (both accounts chosen); a half-filled row is a form error, an empty row is left unmapped.
    /// </summary>
    private JournalMappingLineRequest[] Lines(JournalMappingFormViewModel model)
    {
        foreach (MappingLineInput line in model.Lines.Where(l => l.DebitAccountId is null != l.CreditAccountId is null))
        {
            ModelState.AddModelError(string.Empty, $"Choose both the debit and the credit account of \"{line.Component}\", or leave both empty.");
        }

        JournalMappingLineRequest[] lines =
        [
            .. model.Lines
                .Where(l => l.DebitAccountId is not null && l.CreditAccountId is not null)
                .Select(l => new JournalMappingLineRequest(l.Component, l.DebitAccountId!.Value, l.CreditAccountId!.Value, l.CostCenterId))
        ];

        if (lines.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Map at least one component.");
        }

        return lines;
    }

    private async Task<IReadOnlyList<JournalMappingResponse>> ListAsync(CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<JournalMappingResponse>> result = await mappingsQuery.Handle(new GetJournalMappingsQuery(null), cancellationToken);
        return result.IsSuccess ? result.Value : [];
    }

    private async Task<JournalMappingFormViewModel> WithOptionsAsync(JournalMappingFormViewModel model, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<AccountResponse>> accounts = await accountsQuery.Handle(new GetAccountsQuery(null, null, false), cancellationToken);
        Dictionary<Guid, string> labels = accounts.IsSuccess
            ? accounts.Value.ToDictionary(a => a.Id, a => $"{a.Code} — {a.Name}")
            : [];

        foreach (MappingLineInput line in model.Lines)
        {
            line.DebitAccountLabel = line.DebitAccountId is Guid debit ? labels.GetValueOrDefault(debit) : null;
            line.CreditAccountLabel = line.CreditAccountId is Guid credit ? labels.GetValueOrDefault(credit) : null;
        }

        model.Events = [.. AccountingEvents.All.Select(e => new SelectListItem(AccountingEventLabels.Event(e), e.Code, e.Code == model.EventType))];
        model.Branches = [.. (await support.BranchOptionsAsync(model.BranchId)).Select(b => new SelectListItem(b.Text, b.Value, b.Value == model.BranchId?.ToString()))];
        model.CostCenters = await FinanceOptions.CostCentersAsync(costCentersQuery, model.Lines.Select(l => l.CostCenterId), cancellationToken);

        return model;
    }

    private sealed record MappingRow(string Event, string Branch, string Component, string Debit, string Credit, bool IsActive);
}
