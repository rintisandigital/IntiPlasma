using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Inventory;
using Application.Inventory.StockTransfers;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Inventory.Documents;
using Web.App.Areas.Inventory.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Areas.Inventory.Controllers;

/// <summary>
/// Stock transfers out of a central warehouse (to another central warehouse or to a coop, where the cost is charged
/// to the coop's running cycle). Stock leaves at moving average cost; posted documents cannot be changed.
/// </summary>
[Area("Inventory")]
[MenuAccess(MenuCodes.InventoryStockTransfers)]
public sealed class StockTransfersController(
    PageSupport support,
    InventoryOptions options,
    DisplayFormatter formatter,
    IQueryHandler<GetStockTransfersQuery, PagedList<InventoryDocumentResponse>> transfersQuery,
    IQueryHandler<GetStockTransferByIdQuery, InventoryDocumentResponse> transferQuery) : AppController
{
    private const string MenuCode = MenuCodes.InventoryStockTransfers;

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search, string? branch, DateOnly? from, DateOnly? to, int? page, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<PagedList<InventoryDocumentResponse>> result = await transfersQuery.Handle(
            new GetStockTransfersQuery(new PageRequest(page, PageRequest.DefaultPageSize, search), branchFilter.BranchId, null, null, from, to),
            cancellationToken);

        return View(InventoryLists.ListModel(result.Value, search, branchFilter, from, to));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(
        string? format, string? search, string? branch, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        return await support.ExportAsync(format, "Stock Transfers", "stock-transfers",
            InventoryLists.Filters(branchFilter, from, to, formatter), InventoryLists.Columns("From", null),
            (paging, ct) => transfersQuery.Handle(new GetStockTransfersQuery(paging, branchFilter.BranchId, null, null, from, to), ct),
            search, cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        Result<InventoryDocumentResponse> result = await transferQuery.Handle(new GetStockTransferByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        return View("~/Areas/Inventory/Views/Shared/Document.cshtml", new InventoryDocumentViewModel(
            result.Value,
            await support.AttachmentsAsync(result.Value.Documents, cancellationToken),
            "Stock Transfer",
            "From warehouse",
            await support.CanAsync(MenuCode, MenuRights.Export)));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken) =>
        View("~/Areas/Inventory/Views/Shared/MovementForm.cshtml", await options.PrepareAsync(
            new StockMovementFormViewModel { Date = formatter.Today(), Lines = [new StockLineInput()] },
            StockMovementKind.Transfer,
            cancellationToken));

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        StockMovementFormViewModel model,
        [FromServices] ICommandHandler<CreateStockTransferCommand, CreateStockTransferResponse> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<CreateStockTransferResponse> result = await handler.Handle(
                new CreateStockTransferCommand(
                    model.FromWarehouseId!.Value, model.ToWarehouseId!.Value, model.Date!.Value, model.Notes, model.ToLines(), model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess(result.Value.CycleId is null
                    ? $"Stock transfer {result.Value.Number} has been posted."
                    : $"Stock transfer {result.Value.Number} has been posted and charged to the coop's running cycle.");
                return RedirectToAction(nameof(Details), new { id = result.Value.Id });
            }

            AddErrors(result.Error);
        }

        return View("~/Areas/Inventory/Views/Shared/MovementForm.cshtml",
            await options.PrepareAsync(model, StockMovementKind.Transfer, cancellationToken));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Print(Guid id, CancellationToken cancellationToken)
    {
        Result<InventoryDocumentResponse> result = await transferQuery.Handle(new GetStockTransferByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        InventoryDocumentResponse transfer = result.Value;
        ExportHeader header = await support.Exports.HeaderAsync("Stock Transfer", $"transfer-{transfer.Number}", [$"No. {transfer.Number}"]);
        (string, string)[] fields =
        [
            ("Transfer number", transfer.Number),
            ("Date", formatter.Date(transfer.Date)),
            ("From warehouse", transfer.Reference),
            ("To warehouse", transfer.WarehouseCode),
            ("Cycle", transfer.CycleNumber ?? "—"),
            ("Branch", transfer.BranchCode)
        ];

        return support.Exports.Document(header, container =>
            InventoryDocumentPdf.Compose(container, transfer, fields, ["Issued by", "Delivered by", "Received by"], formatter));
    }
}
