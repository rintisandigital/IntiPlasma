using Application.Abstractions.Messaging;
using Application.Inventory.StockReturns;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Inventory.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Areas.Inventory.Controllers;

/// <summary>
/// Mutasi pakan antar kandang: feed never moves coop to coop directly — it is posted as a return to a central
/// warehouse plus a transfer from there to the other coop (two documents, posted together). The resulting documents
/// are listed in Stock Returns and Stock Transfers.
/// </summary>
[Area("Inventory")]
[MenuAccess(MenuCodes.InventoryFeedMutations)]
public sealed class FeedMutationsController(PageSupport support, InventoryOptions options, DisplayFormatter formatter) : AppController
{
    private const string MenuCode = MenuCodes.InventoryFeedMutations;
    private const string FormView = "~/Areas/Inventory/Views/Shared/MovementForm.cshtml";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        StockMovementFormViewModel model = await options.PrepareAsync(
            new StockMovementFormViewModel { Date = formatter.Today(), Lines = [new StockLineInput()] },
            StockMovementKind.FeedMutation,
            cancellationToken);
        model.CanSave = await support.CanAsync(MenuCode, MenuRights.Create);

        return View(FormView, model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Index(
        StockMovementFormViewModel model,
        [FromServices] ICommandHandler<CreateFeedMutationCommand, CreateFeedMutationResponse> handler,
        CancellationToken cancellationToken)
    {
        if (model.ViaWarehouseId is null)
        {
            ModelState.AddModelError(nameof(model.ViaWarehouseId), "Choose the central warehouse the feed passes through.");
        }

        if (string.IsNullOrWhiteSpace(model.Reason))
        {
            ModelState.AddModelError(nameof(model.Reason), "Enter the reason of the mutation.");
        }

        if (ModelState.IsValid)
        {
            Result<CreateFeedMutationResponse> result = await handler.Handle(
                new CreateFeedMutationCommand(
                    model.FromWarehouseId!.Value, model.ViaWarehouseId!.Value, model.ToWarehouseId!.Value, model.Date!.Value,
                    model.Reason!, model.ToLines(), model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Feed mutation posted: return {result.Value.ReturnNumber} and transfer {result.Value.TransferNumber}.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        return View(FormView, await options.PrepareAsync(model, StockMovementKind.FeedMutation, cancellationToken));
    }
}
