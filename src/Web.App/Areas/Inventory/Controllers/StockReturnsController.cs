using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Inventory;
using Application.Inventory.StockReturns;
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
/// Stock returns (retur sapronak): leftover feed/OVK from a coop warehouse back to a central warehouse, taken out of
/// the coop's running cycle at its moving average cost. Posted documents cannot be changed.
/// </summary>
[Area("Inventory")]
[MenuAccess(MenuCodes.InventoryStockReturns)]
public sealed class StockReturnsController(
    PageSupport support,
    InventoryOptions options,
    DisplayFormatter formatter,
    IQueryHandler<GetStockReturnsQuery, PagedList<InventoryDocumentResponse>> returnsQuery,
    IQueryHandler<GetStockReturnByIdQuery, InventoryDocumentResponse> returnQuery) : AppController
{
    private const string MenuCode = MenuCodes.InventoryStockReturns;

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search, string? branch, DateOnly? from, DateOnly? to, int? page, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<PagedList<InventoryDocumentResponse>> result = await returnsQuery.Handle(
            new GetStockReturnsQuery(new PageRequest(page, PageRequest.DefaultPageSize, search), branchFilter.BranchId, null, null, from, to),
            cancellationToken);

        return View(InventoryLists.ListModel(result.Value, search, branchFilter, from, to));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(
        string? format, string? search, string? branch, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        return await support.ExportAsync(format, "Stock Returns", "stock-returns",
            InventoryLists.Filters(branchFilter, from, to, formatter), InventoryLists.Columns("From", "Reason"),
            (paging, ct) => returnsQuery.Handle(new GetStockReturnsQuery(paging, branchFilter.BranchId, null, null, from, to), ct),
            search, cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        Result<InventoryDocumentResponse> result = await returnQuery.Handle(new GetStockReturnByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        return View("~/Areas/Inventory/Views/Shared/Document.cshtml", new InventoryDocumentViewModel(
            result.Value,
            await support.AttachmentsAsync(result.Value.Documents, cancellationToken),
            "Stock Return",
            "From coop warehouse",
            await support.CanAsync(MenuCode, MenuRights.Export)));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken) =>
        View("~/Areas/Inventory/Views/Shared/MovementForm.cshtml", await options.PrepareAsync(
            new StockMovementFormViewModel { Date = formatter.Today(), Lines = [new StockLineInput()] },
            StockMovementKind.Return,
            cancellationToken));

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        StockMovementFormViewModel model,
        [FromServices] ICommandHandler<CreateStockReturnCommand, CreateStockReturnResponse> handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Reason))
        {
            ModelState.AddModelError(nameof(model.Reason), "Enter the reason of the return.");
        }

        if (ModelState.IsValid)
        {
            Result<CreateStockReturnResponse> result = await handler.Handle(
                new CreateStockReturnCommand(
                    model.FromWarehouseId!.Value, model.ToWarehouseId!.Value, model.Date!.Value, model.Reason!, model.Notes,
                    model.ToLines(), model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Stock return {result.Value.Number} has been posted and taken out of the coop's running cycle.");
                return RedirectToAction(nameof(Details), new { id = result.Value.Id });
            }

            AddErrors(result.Error);
        }

        return View("~/Areas/Inventory/Views/Shared/MovementForm.cshtml",
            await options.PrepareAsync(model, StockMovementKind.Return, cancellationToken));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Print(Guid id, CancellationToken cancellationToken)
    {
        Result<InventoryDocumentResponse> result = await returnQuery.Handle(new GetStockReturnByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        InventoryDocumentResponse stockReturn = result.Value;
        ExportHeader header = await support.Exports.HeaderAsync("Stock Return", $"return-{stockReturn.Number}", [$"No. {stockReturn.Number}"]);
        (string, string)[] fields =
        [
            ("Return number", stockReturn.Number),
            ("Date", formatter.Date(stockReturn.Date)),
            ("From coop warehouse", stockReturn.Reference),
            ("To warehouse", stockReturn.WarehouseCode),
            ("Cycle", stockReturn.CycleNumber ?? "—"),
            ("Reason", stockReturn.Party ?? "—")
        ];

        return support.Exports.Document(header, container =>
            InventoryDocumentPdf.Compose(container, stockReturn, fields, ["Returned by (coop)", "Delivered by", "Received by"], formatter));
    }
}
