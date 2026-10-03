using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Coops;
using Application.Coops.Create;
using Application.Coops.Get;
using Application.Coops.GetById;
using Application.Coops.Update;
using Application.Farmers;
using Application.Farmers.GetById;
using Domain.Access;
using Domain.MasterData.Coops;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.MasterData.Models;
using Web.App.Areas.Partnership.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;

namespace Web.App.Areas.Partnership.Controllers;

/// <summary>
/// Coops (kandang) of a farmer; the coop warehouse GK-{code} is created automatically (outbox).
/// </summary>
[Area("Partnership")]
[MenuAccess(MenuCodes.PartnershipCoops)]
public sealed class CoopsController(
    PageSupport support,
    IQueryHandler<GetCoopsQuery, PagedList<CoopResponse>> coopsQuery,
    IQueryHandler<GetFarmerByIdQuery, FarmerResponse> farmerQuery) : AppController
{
    private const string MenuCode = MenuCodes.PartnershipCoops;

    private static readonly ExportColumn<CoopResponse>[] Columns =
    [
        new("Code", c => c.Code, Width: 1.2f),
        new("Name", c => c.Name, Width: 2.5f),
        new("Farmer", c => $"{c.FarmerCode} — {c.FarmerName}", Width: 2.5f),
        new("Farmer type", c => c.FarmerType, Width: 0.9f),
        new("Branch", c => c.BranchCode, Width: 0.8f),
        new("Capacity", c => c.Capacity, ExportFormat.WholeNumber, 1),
        new("House type", c => c.HouseType, Width: 1.1f),
        new("Running cycle", c => c.OpenCycleId is not null, ExportFormat.Boolean, 0.9f),
        new("Address", c => c.Address, Width: 2.5f),
        new("Active", c => c.IsActive, ExportFormat.Boolean, 0.6f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? branch, Guid? farmerId, int? page, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<PagedList<CoopResponse>> result = await coopsQuery.Handle(
            new GetCoopsQuery(new PageRequest(page, PageRequest.DefaultPageSize, search), branchFilter.BranchId, farmerId),
            cancellationToken);

        return View(new ListViewModel<CoopResponse>
        {
            Rows = result.Value,
            Search = search,
            BranchOptions = branchFilter.Options,
            Filters = new Dictionary<string, string?> { ["farmerId"] = farmerId?.ToString() }
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(string? format, string? search, string? branch, Guid? farmerId, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        return await support.ExportAsync(format, "Coops", "coops", [branchFilter.Description], Columns,
            (paging, ct) => coopsQuery.Handle(new GetCoopsQuery(paging, branchFilter.BranchId, farmerId), ct), search, cancellationToken);
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(Guid? farmerId, CancellationToken cancellationToken) =>
        View("Form", await WithFarmerLabelAsync(
            new CoopFormViewModel { FarmerId = farmerId, HouseType = HouseType.ClosedHouse }, cancellationToken));

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        CoopFormViewModel model,
        [FromServices] ICommandHandler<CreateCoopCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(
                new CreateCoopCommand(
                    model.FarmerId!.Value, model.Code, model.Name, model.Capacity!.Value, model.HouseType!.Value,
                    model.Address, model.Latitude, model.Longitude, model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Coop {model.Code} has been created; its coop warehouse is being set up.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        model.Attachments = await support.AttachmentsAsync(model.Documents, cancellationToken);
        return View("Form", await WithFarmerLabelAsync(model, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(
        Guid id,
        [FromServices] IQueryHandler<GetCoopByIdQuery, CoopResponse> query,
        CancellationToken cancellationToken)
    {
        Result<CoopResponse> result = await query.Handle(new GetCoopByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        CoopResponse coop = result.Value;

        return View("Form", new CoopFormViewModel
        {
            Id = coop.Id,
            FarmerId = coop.FarmerId,
            FarmerLabel = $"{coop.FarmerCode} — {coop.FarmerName} ({coop.FarmerType})",
            BranchCode = coop.BranchCode,
            Code = coop.Code,
            Name = coop.Name,
            Capacity = coop.Capacity,
            HouseType = Enum.Parse<HouseType>(coop.HouseType),
            Address = coop.Address,
            Latitude = coop.Latitude,
            Longitude = coop.Longitude,
            IsActive = coop.IsActive,
            OpenCycleId = coop.OpenCycleId,
            Documents = [.. coop.Documents],
            Attachments = await support.AttachmentsAsync(coop.Documents, cancellationToken),
            CanSave = await support.CanAsync(MenuCode, MenuRights.Edit)
        });
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        CoopFormViewModel model,
        [FromServices] ICommandHandler<UpdateCoopCommand> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid && model.Id is Guid id)
        {
            Result result = await handler.Handle(
                new UpdateCoopCommand(
                    id, model.Name, model.Capacity!.Value, model.HouseType!.Value, model.Address, model.Latitude,
                    model.Longitude, model.IsActive == true, model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Coop {model.Code} has been saved.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        model.Attachments = await support.AttachmentsAsync(model.Documents, cancellationToken);
        return View("Form", model);
    }

    /// <summary>
    /// The farmer lookup only posts the id; the chosen farmer is re-rendered as the selected option.
    /// </summary>
    private async Task<CoopFormViewModel> WithFarmerLabelAsync(CoopFormViewModel model, CancellationToken cancellationToken)
    {
        if (model.FarmerId is Guid farmerId)
        {
            Result<FarmerResponse> farmer = await farmerQuery.Handle(new GetFarmerByIdQuery(farmerId), cancellationToken);
            model.FarmerLabel = farmer.IsSuccess
                ? $"{farmer.Value.Code} — {farmer.Value.Name} ({farmer.Value.Type}, {farmer.Value.BranchCode})"
                : null;
        }

        return model;
    }
}
