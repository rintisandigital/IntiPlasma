using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Approvals;
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
/// Farmers (peternak): Inti or Plasma (plasma needs a NIK), per branch, with bank account for settlements. New
/// farmers and changes of the NIK or bank account of an approved farmer go through the approval module (M-81, M-84);
/// the edit page doubles as the details page, with the approval timeline.
/// </summary>
[Area("Partnership")]
[MenuAccess(MenuCodes.PartnershipFarmers)]
public sealed class FarmersController(
    PageSupport support,
    IQueryHandler<GetFarmersQuery, PagedList<FarmerResponse>> farmersQuery,
    IQueryHandler<GetFarmerByIdQuery, FarmerResponse> farmerQuery,
    IQueryHandler<GetFarmerApprovalsQuery, IReadOnlyList<ApprovalDetailResponse>> approvalsQuery) : AppController
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
        new("PPL", f => f.FieldOfficerName, Width: 1.5f),
        new("Status", f => f.RegistrationStatus, Width: 1.1f),
        new("Active", f => f.IsActive, ExportFormat.Boolean, 0.6f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? branch,
        FarmerType? type,
        Guid? fieldOfficerId,
        int? page,
        CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<PagedList<FarmerResponse>> result = await farmersQuery.Handle(
            new GetFarmersQuery(
                new PageRequest(page, PageRequest.DefaultPageSize, search), branchFilter.BranchId, type, fieldOfficerId),
            cancellationToken);

        return View(new ListViewModel<FarmerResponse>
        {
            Rows = result.Value,
            Search = search,
            BranchOptions = branchFilter.Options,
            FilterOptions = new Dictionary<string, IReadOnlyList<SelectListItem>>
            {
                ["type"] = EnumOptions.For(type),
                ["fieldOfficerId"] = await support.FieldOfficerOptionsAsync(branchFilter.BranchId, fieldOfficerId, cancellationToken)
            }
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(
        string? format,
        string? search,
        string? branch,
        FarmerType? type,
        Guid? fieldOfficerId,
        CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);
        List<string> filters = [branchFilter.Description];
        if (type is not null)
        {
            filters.Add($"Type: {type}");
        }

        if (fieldOfficerId is not null)
        {
            filters.Add("One PPL");
        }

        return await support.ExportAsync(format, "Farmers", "farmers", filters, Columns,
            (paging, ct) => farmersQuery.Handle(new GetFarmersQuery(paging, branchFilter.BranchId, type, fieldOfficerId), ct),
            search, cancellationToken);
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
                    model.Address, model.Phone, model.BankAccount.ToRequest(), model.Documents, model.FieldOfficerUserId),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Farmer {model.Code} has been created and submitted for approval.");
                return RedirectToAction(nameof(Edit), new { id = result.Value });
            }

            AddErrors(result.Error);
        }

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    [HttpGet]
    public Task<IActionResult> Details(Guid id, CancellationToken cancellationToken) => Edit(id, cancellationToken);

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        Result<FarmerResponse> result = await farmerQuery.Handle(new GetFarmerByIdQuery(id), cancellationToken);

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
            FieldOfficerUserId = farmer.FieldOfficerUserId,
            FieldOfficerLabel = farmer.FieldOfficerName
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
                    model.BankAccount.ToRequest(), model.IsActive == true, model.Documents, model.FieldOfficerUserId),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Farmer {model.Code} has been saved. A new NIK or bank account takes effect once approved.");
                return RedirectToAction(nameof(Edit), new { id });
            }

            AddErrors(result.Error);
        }

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    /// <summary>
    /// Submits a draft or rejected farmer again (M-83).
    /// </summary>
    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Submit(
        Guid id,
        [FromServices] ICommandHandler<SubmitFarmerCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        Result<Guid> result = await handler.Handle(new SubmitFarmerCommand(id), cancellationToken);

        if (result.IsSuccess)
        {
            NotifySuccess("The farmer has been submitted for approval.");
        }
        else
        {
            NotifyError(result.Error.Description);
        }

        return RedirectToAction(nameof(Edit), new { id });
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

        if (model.Id is Guid id)
        {
            bool canEdit = await support.CanAsync(MenuCode, MenuRights.Edit);
            Result<FarmerResponse> farmer = await farmerQuery.Handle(new GetFarmerByIdQuery(id), cancellationToken);
            Result<IReadOnlyList<ApprovalDetailResponse>> approvals =
                await approvalsQuery.Handle(new GetFarmerApprovalsQuery(id), cancellationToken);

            model.RegistrationStatus = farmer.IsSuccess ? farmer.Value.RegistrationStatus : null;
            model.Approvals = approvals.IsSuccess ? approvals.Value : [];
            model.CanSave = canEdit && model.RegistrationStatus != "PendingApproval";
            model.CanSubmit = canEdit && model.RegistrationStatus is "Draft" or "Rejected";
        }
        model.Attachments = await support.AttachmentsAsync(model.Documents, cancellationToken);
        model.FieldOfficerLabel ??= await support.FieldOfficerLabelAsync(model.BranchId, model.FieldOfficerUserId, cancellationToken);
        return model;
    }
}
