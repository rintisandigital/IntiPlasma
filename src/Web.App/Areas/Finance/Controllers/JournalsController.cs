using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Documents;
using Application.Finance.CostCenters;
using Application.Finance.Journals;
using Application.Finance.JournalTemplates;
using Application.Inventory;
using Domain.Access;
using Domain.Documents.Attachments;
using Domain.Finance.Journals;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;
using Web.App.Areas.Finance.Documents;
using Web.App.Areas.Finance.Models;
using Web.App.Areas.MasterData.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;
using Web.App.Infrastructure.Workflow;

namespace Web.App.Areas.Finance.Controllers;

/// <summary>
/// General journal: manual journals (Draft maker → Approved by someone else → Posted with a number in an open period;
/// a posted journal is corrected by a reversal) and the automatic journals of the business documents, read-only.
/// </summary>
[Area("Finance")]
[MenuAccess(MenuCodes.FinanceJournals)]
public sealed class JournalsController(
    PageSupport support,
    DisplayFormatter formatter,
    IQueryHandler<GetJournalsQuery, PagedList<JournalResponse>> journalsQuery,
    IQueryHandler<GetJournalByIdQuery, JournalResponse> journalQuery,
    IQueryHandler<GetCostCentersQuery, IReadOnlyList<CostCenterResponse>> costCentersQuery,
    IQueryHandler<GetJournalTemplatesQuery, IReadOnlyList<JournalTemplateResponse>> templatesQuery) : AppController
{
    private const string MenuCode = MenuCodes.FinanceJournals;
    private const string DocumentType = "Journal";

    private static readonly ExportColumn<JournalResponse>[] Columns =
    [
        new("Number", j => j.Number ?? "(draft)", Width: 1.5f),
        new("Date", j => j.Date, ExportFormat.Date, 0.9f),
        new("Branch", j => j.BranchCode, Width: 0.7f),
        new("Source", j => JournalSources.Label(j.SourceType), Width: 1.3f),
        new("Description", j => j.Description, Width: 3),
        new("Debit", j => j.TotalDebit, ExportFormat.Money, 1.3f),
        new("Credit", j => j.TotalCredit, ExportFormat.Money, 1.3f),
        new("Status", j => j.Status, Width: 0.8f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search, string? branch, JournalStatus? status, JournalSource? source, DateOnly? from, DateOnly? to, int? page,
        CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<PagedList<JournalResponse>> result = await journalsQuery.Handle(
            new GetJournalsQuery(new PageRequest(page, PageRequest.DefaultPageSize, search), branchFilter.BranchId, status, source, from, to),
            cancellationToken);

        return View(new ListViewModel<JournalResponse>
        {
            Rows = result.Value,
            Search = search,
            BranchOptions = branchFilter.Options,
            Filters = DocumentLists.DateFilters(from, to),
            FilterOptions = new Dictionary<string, IReadOnlyList<SelectListItem>>
            {
                ["status"] = EnumOptions.For(status),
                ["source"] = EnumOptions.For(source)
            }
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(
        string? format, string? search, string? branch, JournalStatus? status, JournalSource? source, DateOnly? from, DateOnly? to,
        CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        List<string> filters = DocumentLists.Filters(branchFilter, status?.ToString(), from, to, formatter);
        if (source is not null)
        {
            filters.Add($"Source: {source}");
        }

        return await support.ExportAsync(format, "Journals", "journals", filters, Columns,
            (paging, ct) => journalsQuery.Handle(new GetJournalsQuery(paging, branchFilter.BranchId, status, source, from, to), ct),
            search, cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        Result<JournalResponse> result = await journalQuery.Handle(new GetJournalByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        JournalResponse journal = result.Value;
        string? reversalNumber = null;
        if (journal.ReversedById is Guid reversalId)
        {
            Result<JournalResponse> reversal = await journalQuery.Handle(new GetJournalByIdQuery(reversalId), cancellationToken);
            reversalNumber = reversal.IsSuccess ? reversal.Value.Number : null;
        }

        return View(new JournalDetailsViewModel(
            journal,
            await support.AttachmentsAsync(journal.Documents, cancellationToken),
            SourceUrl(journal),
            reversalNumber,
            CanEdit: await support.CanAsync(MenuCode, MenuRights.Edit),
            CanDelete: await support.CanAsync(MenuCode, MenuRights.Delete),
            CanPrint: await support.CanAsync(MenuCode, MenuRights.Export)));
    }

    /// <summary>
    /// Opens the business document of an automatic journal whose source is ambiguous (sapronak charged to a cycle by a
    /// stock transfer or by a goods receipt straight into the coop warehouse).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Source(
        Guid id,
        [FromServices] IQueryHandler<GetStockTransferByIdQuery, InventoryDocumentResponse> transferQuery,
        CancellationToken cancellationToken)
    {
        Result<JournalResponse> result = await journalQuery.Handle(new GetJournalByIdQuery(id), cancellationToken);
        if (result.IsFailure || result.Value.SourceId is not Guid sourceId)
        {
            return NotFound();
        }

        Result<InventoryDocumentResponse> transfer = await transferQuery.Handle(new GetStockTransferByIdQuery(sourceId), cancellationToken);

        return transfer.IsSuccess
            ? RedirectToAction("Details", "StockTransfers", new { area = "Inventory", id = sourceId })
            : RedirectToAction("Details", "GoodsReceipts", new { area = "Inventory", id = sourceId });
    }

    /// <param name="templateId">Journal template whose accounts, sides and cost centers prefill the lines.</param>
    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(Guid? templateId, Guid? branchId, CancellationToken cancellationToken)
    {
        var model = new JournalFormViewModel { BranchId = branchId, Date = formatter.Today() };

        IReadOnlyList<JournalTemplateResponse> templates = await TemplatesAsync(cancellationToken);
        if (templates.FirstOrDefault(t => t.Id == templateId) is { } template)
        {
            model.Description = template.Description ?? template.Name;
            model.Lines =
            [
                .. template.Lines.OrderBy(l => l.LineNumber).Select(l => new JournalFormLineInput
                {
                    AccountId = l.AccountId,
                    AccountLabel = $"{l.AccountCode} — {l.AccountName}",
                    CostCenterId = l.CostCenterId,
                    Description = l.Description,
                    Debit = l.Side == "Debit" ? 0 : null,
                    Credit = l.Side == "Credit" ? 0 : null
                })
            ];
        }
        else
        {
            model.Lines = [new JournalFormLineInput(), new JournalFormLineInput()];
        }

        await PrepareAsync(model, templates, templateId, cancellationToken);
        return View("Form", model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        JournalFormViewModel model,
        [FromServices] ICommandHandler<CreateJournalCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        model.Id = null;
        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(
                new CreateJournalCommand(model.BranchId!.Value, model.Date!.Value, model.Description, model.ToLines(), model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess("The journal has been saved as a draft; another user must approve it before it is posted.");
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }

            AddErrors(result.Error);
        }

        await PrepareAsync(model, await TemplatesAsync(cancellationToken), null, cancellationToken);
        return View("Form", model);
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        Result<JournalResponse> result = await journalQuery.Handle(new GetJournalByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        JournalResponse journal = result.Value;
        if (journal.Status != nameof(JournalStatus.Draft) || journal.Source != nameof(JournalSource.Manual))
        {
            NotifyError("Only a manual journal draft can be edited.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new JournalFormViewModel
        {
            Id = id,
            BranchId = journal.BranchId,
            Date = journal.Date,
            Description = journal.Description,
            Documents = [.. journal.Documents],
            Lines =
            [
                .. (journal.Lines ?? []).Select(l => new JournalFormLineInput
                {
                    AccountId = l.AccountId,
                    AccountLabel = $"{l.AccountCode} — {l.AccountName}",
                    CostCenterId = l.CostCenterId,
                    Description = l.Description,
                    Debit = l.Debit == 0 ? null : l.Debit,
                    Credit = l.Credit == 0 ? null : l.Credit
                })
            ]
        };

        await PrepareAsync(model, [], null, cancellationToken);
        return View("Form", model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        Guid id,
        JournalFormViewModel model,
        [FromServices] ICommandHandler<UpdateJournalCommand> handler,
        CancellationToken cancellationToken)
    {
        model.Id = id;
        if (ModelState.IsValid)
        {
            Result result = await handler.Handle(
                new UpdateJournalCommand(id, model.Date!.Value, model.Description, model.ToLines(), model.Documents), cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess("The journal draft has been saved.");
                return RedirectToAction(nameof(Details), new { id });
            }

            AddErrors(result.Error);
        }

        await PrepareAsync(model, [], null, cancellationToken);
        return View("Form", model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Delete)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] ICommandHandler<DeleteJournalCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new DeleteJournalCommand(id), cancellationToken);
        if (result.IsFailure)
        {
            NotifyError(result.Error.Description);
            return RedirectToAction(nameof(Details), new { id });
        }

        NotifySuccess("The journal draft has been deleted.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Checker approval: the domain refuses the maker approving their own journal.
    /// </summary>
    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("approve")]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<ApproveJournalCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "approve"),
            ct => handler.Handle(new ApproveJournalCommand(id), ct),
            cancellationToken);

        return AfterAction(id, result, "The journal has been approved; it can now be posted.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("post")]
    public async Task<IActionResult> Post(
        Guid id,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<PostJournalCommand, string> handler,
        CancellationToken cancellationToken)
    {
        string? number = null;
        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "post"),
            async ct =>
            {
                Result<string> posted = await handler.Handle(new PostJournalCommand(id), ct);
                number = posted.IsSuccess ? posted.Value : null;
                return posted;
            },
            cancellationToken);

        return AfterAction(id, result, $"The journal has been posted as {number}.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("reverse")]
    public async Task<IActionResult> Reverse(
        Guid id,
        DateOnly? date,
        string? reason,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<ReverseJournalCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            NotifyError("Enter the reason for reversing.");
            return RedirectToAction(nameof(Details), new { id });
        }

        Guid? reversalId = null;
        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "reverse"),
            async ct =>
            {
                Result<Guid> reversed = await handler.Handle(new ReverseJournalCommand(id, date ?? formatter.Today(), reason), ct);
                reversalId = reversed.IsSuccess ? reversed.Value : null;
                return reversed;
            },
            cancellationToken);

        if (result.IsSuccess && reversalId is not null)
        {
            NotifySuccess("The journal has been reversed; this is the reversal journal.");
            return RedirectToAction(nameof(Details), new { id = reversalId });
        }

        return AfterAction(id, result, "The journal has been reversed.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Documents(
        Guid id,
        List<Guid> documents,
        [FromServices] ICommandHandler<SetDocumentsCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new SetDocumentsCommand(AttachmentOwnerTypes.Journal, id, documents), cancellationToken);
        return AfterAction(id, result, "The attachments have been saved.");
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Print(Guid id, CancellationToken cancellationToken)
    {
        Result<JournalResponse> result = await journalQuery.Handle(new GetJournalByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        JournalResponse journal = result.Value;
        string title = journal.Status is nameof(JournalStatus.Draft) or nameof(JournalStatus.Approved)
            ? "Journal Voucher (NOT POSTED)"
            : "Journal Voucher";
        string reference = journal.Number ?? "draft";
        ExportHeader header = await support.Exports.HeaderAsync(title, $"journal-{reference}", [$"No. {reference}"]);

        return support.Exports.Document(header, container => FinanceDocumentPdf.JournalVoucher(container, journal, formatter));
    }

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

    private string? SourceUrl(JournalResponse journal)
    {
        if (journal.SourceId is not Guid sourceId)
        {
            return null;
        }

        if (journal.SourceType == "StockTransferToCycle")
        {
            return Url.Action(nameof(Source), new { id = journal.Id });
        }

        if (journal.SourceType is "YearEndClosing" or "YearEndClosing.Reversal")
        {
            return Url.Action("Index", "FiscalPeriods", new { area = "Finance", year = journal.Date.Year });
        }

        return JournalSources.Route(journal.SourceType) is { } route
            ? Url.Action(route.Action, route.Controller, new { area = route.Area, id = sourceId })
            : null;
    }

    private async Task<IReadOnlyList<JournalTemplateResponse>> TemplatesAsync(CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<JournalTemplateResponse>> result = await templatesQuery.Handle(new GetJournalTemplatesQuery(), cancellationToken);
        return result.IsSuccess ? [.. result.Value.Where(t => t.IsActive)] : [];
    }

    private async Task PrepareAsync(
        JournalFormViewModel model,
        IReadOnlyList<JournalTemplateResponse> templates,
        Guid? templateId,
        CancellationToken cancellationToken)
    {
        model.Branches = await support.BranchOptionsAsync(model.BranchId);
        model.BranchId ??= Guid.TryParse(model.Branches.FirstOrDefault(b => b.Selected)?.Value, out Guid branchId) ? branchId : null;
        model.CostCenters = await FinanceOptions.CostCentersAsync(costCentersQuery, model.Lines.Select(l => l.CostCenterId), cancellationToken);
        model.Templates = [.. templates.Select(t => new SelectListItem(t.Name, t.Id.ToString(), t.Id == templateId))];
        model.Attachments = await support.AttachmentsAsync(model.Documents, cancellationToken);
    }
}
