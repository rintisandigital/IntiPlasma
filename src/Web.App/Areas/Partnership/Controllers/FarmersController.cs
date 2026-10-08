using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Documents;
using Application.Farmers;
using Application.Farmers.Create;
using Application.Farmers.Get;
using Application.Farmers.GetById;
using Application.Farmers.Update;
using Domain.Access;
using Domain.MasterData.Farmers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;
using Web.App.Areas.MasterData.Models;
using Web.App.Areas.Partnership.Documents;
using Web.App.Areas.Partnership.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;
using Web.App.Models.Shared;

namespace Web.App.Areas.Partnership.Controllers;

/// <summary>
/// Farmers (peternak): Inti or Plasma (plasma needs a NIK), per branch, with bank account for settlements.
/// </summary>
[Area("Partnership")]
[MenuAccess(MenuCodes.PartnershipFarmers)]
public sealed class FarmersController(
    PageSupport support,
    IQueryHandler<GetFarmersQuery, PagedList<FarmerResponse>> farmersQuery) : AppController
{
    private const string MenuCode = MenuCodes.PartnershipFarmers;

    private static readonly ExportColumn<FarmerResponse>[] Columns =
    [
        new("Code", f => f.Code, Width: 1.2f),
        new("Name", f => f.Name, Width: 3),
        new("Type", f => f.Type, Width: 0.8f),
        new("Branch", f => f.BranchCode, Width: 0.8f),
        new("NIK", f => f.Nik, Width: 1.8f),
        new("NPWP", f => f.TaxIdentity.Npwp, Width: 1.8f),
        new("Phone", f => f.Phone, Width: 1.3f),
        new("Bank", f => f.BankAccount.BankName, Width: 1.2f),
        new("Account", f => f.BankAccount.AccountNumber, Width: 1.5f),
        new("Farms", f => f.CoopCount, ExportFormat.WholeNumber, 0.7f),
        new("Active", f => f.IsActive, ExportFormat.Boolean, 0.6f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? branch, FarmerType? type, int? page, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<PagedList<FarmerResponse>> result = await farmersQuery.Handle(
            new GetFarmersQuery(new PageRequest(page, PageRequest.DefaultPageSize, search), branchFilter.BranchId, type),
            cancellationToken);

        return View(new ListViewModel<FarmerResponse>
        {
            Rows = result.Value,
            Search = search,
            BranchOptions = branchFilter.Options,
            FilterOptions = new Dictionary<string, IReadOnlyList<SelectListItem>> { ["type"] = EnumOptions.For(type) }
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(string? format, string? search, string? branch, FarmerType? type, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);
        List<string> filters = [branchFilter.Description];
        if (type is not null)
        {
            filters.Add($"Type: {type}");
        }

        return await support.ExportAsync(format, "Farmers", "farmers", filters, Columns,
            (paging, ct) => farmersQuery.Handle(new GetFarmersQuery(paging, branchFilter.BranchId, type), ct), search, cancellationToken);
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create() =>
        View("Form", new FarmerFormViewModel { Type = FarmerType.Plasma, Branches = await support.BranchOptionsAsync(null) });

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        FarmerFormViewModel model,
        [FromServices] ICommandHandler<CreateFarmerCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(
                new CreateFarmerCommand(
                    model.Code, model.Name, model.Type!.Value, model.BranchId!.Value, model.Nik, model.TaxIdentity.ToRequest(),
                    model.Address, model.Phone, model.BankAccount.ToRequest(), model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Farmer {model.Code} has been created.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(
        Guid id,
        [FromServices] IQueryHandler<GetFarmerByIdQuery, FarmerResponse> query,
        CancellationToken cancellationToken)
    {
        Result<FarmerResponse> result = await query.Handle(new GetFarmerByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        FarmerResponse farmer = result.Value;
        var model = new FarmerFormViewModel
        {
            Id = farmer.Id,
            Code = farmer.Code,
            Name = farmer.Name,
            Type = Enum.Parse<FarmerType>(farmer.Type),
            BranchId = farmer.BranchId,
            Nik = farmer.Nik,
            TaxIdentity = TaxIdentityInput.From(farmer.TaxIdentity),
            Address = farmer.Address,
            Phone = farmer.Phone,
            BankAccount = BankAccountInput.From(farmer.BankAccount),
            IsActive = farmer.IsActive,
            CoopCount = farmer.CoopCount,
            Documents = [.. farmer.Documents],
            CanSave = await support.CanAsync(MenuCode, MenuRights.Edit)
        };

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        FarmerFormViewModel model,
        [FromServices] ICommandHandler<UpdateFarmerCommand> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid && model.Id is Guid id)
        {
            Result result = await handler.Handle(
                new UpdateFarmerCommand(
                    id, model.Name, model.Nik, model.TaxIdentity.ToRequest(), model.Address, model.Phone,
                    model.BankAccount.ToRequest(), model.IsActive == true, model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Farmer {model.Code} has been saved.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    /// <summary>
    /// Farmer data sheet as PDF: page 1 the farmer, tax and bank data, page 2 the photo attachments.
    /// </summary>
    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Print(
        Guid id,
        [FromServices] IQueryHandler<GetFarmerByIdQuery, FarmerResponse> query,
        [FromServices] IAttachmentService attachments,
        [FromServices] DisplayFormatter formatter,
        CancellationToken cancellationToken)
    {
        Result<FarmerResponse> result = await query.Handle(new GetFarmerByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        FarmerResponse farmer = result.Value;
        IReadOnlyList<DataSheetPdf.Photo> photos = await DataSheetPdf.PhotosAsync(attachments, farmer.Documents, cancellationToken);
        ExportHeader header = await support.Exports.HeaderAsync(
            FarmerPdf.DataTitle, $"farmer-{farmer.Code}", [$"Kode {farmer.Code}", farmer.Name]);

        return support.Exports.Document(header,
        [
            (FarmerPdf.DataTitle, container => FarmerPdf.ComposeData(container, farmer, formatter)),
            (DataSheetPdf.GalleryTitle, container => FarmerPdf.ComposeGallery(container, farmer, photos))
        ]);
    }

    private async Task<FarmerFormViewModel> WithOptionsAsync(FarmerFormViewModel model, CancellationToken cancellationToken)
    {
        model.Branches = await support.BranchOptionsAsync(model.BranchId);
        model.Attachments = await support.AttachmentsAsync(model.Documents, cancellationToken);
        return model;
    }
}
