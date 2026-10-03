using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Vendors;
using Application.Vendors.Create;
using Application.Vendors.Get;
using Application.Vendors.GetById;
using Application.Vendors.Update;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.MasterData.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Models.Shared;

namespace Web.App.Areas.MasterData.Controllers;

/// <summary>
/// Sapronak suppliers: tax identity, payment term, bank account and the accepted invoice price tolerance.
/// </summary>
[Area("MasterData")]
[MenuAccess(MenuCodes.MasterVendors)]
public sealed class VendorsController(
    PageSupport support,
    IQueryHandler<GetVendorsQuery, PagedList<VendorResponse>> vendorsQuery) : AppController
{
    private const string MenuCode = MenuCodes.MasterVendors;

    private static readonly ExportColumn<VendorResponse>[] Columns =
    [
        new("Code", v => v.Code, Width: 1.2f),
        new("Name", v => v.Name, Width: 3),
        new("NPWP", v => v.TaxIdentity.Npwp, Width: 1.8f),
        new("PKP", v => v.TaxIdentity.IsPkp, ExportFormat.Boolean, 0.6f),
        new("Phone", v => v.Phone, Width: 1.3f),
        new("Email", v => v.Email, Width: 2),
        new("Term (days)", v => v.PaymentTermDays, ExportFormat.WholeNumber, 0.8f),
        new("Price tolerance %", v => v.PriceTolerancePercent, ExportFormat.Percent, 1),
        new("Bank", v => v.BankAccount.BankName, Width: 1.2f),
        new("Account", v => v.BankAccount.AccountNumber, Width: 1.5f),
        new("Active", v => v.IsActive, ExportFormat.Boolean, 0.6f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int? page, CancellationToken cancellationToken)
    {
        Result<PagedList<VendorResponse>> result = await vendorsQuery.Handle(
            new GetVendorsQuery(new PageRequest(page, PageRequest.DefaultPageSize, search)), cancellationToken);

        return View(new ListViewModel<VendorResponse> { Rows = result.Value, Search = search });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public Task<IActionResult> Export(string? format, string? search, CancellationToken cancellationToken) =>
        support.ExportAsync(format, "Vendors", "vendors", string.IsNullOrWhiteSpace(search) ? [] : [$"Search: {search}"], Columns,
            (paging, ct) => vendorsQuery.Handle(new GetVendorsQuery(paging), ct), search, cancellationToken);

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public IActionResult Create() => View("Form", new VendorFormViewModel { PaymentTermDays = 30, PriceTolerancePercent = 0 });

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        VendorFormViewModel model,
        [FromServices] ICommandHandler<CreateVendorCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(
                new CreateVendorCommand(
                    model.Code, model.Name, model.TaxIdentity.ToRequest(), model.Address, model.Phone, model.Email,
                    model.PaymentTermDays!.Value, model.BankAccount.ToRequest(), model.PriceTolerancePercent ?? 0, model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Vendor {model.Code} has been created.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        model.Attachments = await support.AttachmentsAsync(model.Documents, cancellationToken);
        return View("Form", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(
        Guid id,
        [FromServices] IQueryHandler<GetVendorByIdQuery, VendorResponse> query,
        CancellationToken cancellationToken)
    {
        Result<VendorResponse> result = await query.Handle(new GetVendorByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return NotFound();
        }

        VendorResponse vendor = result.Value;

        return View("Form", new VendorFormViewModel
        {
            Id = vendor.Id,
            Code = vendor.Code,
            Name = vendor.Name,
            TaxIdentity = TaxIdentityInput.From(vendor.TaxIdentity),
            Address = vendor.Address,
            Phone = vendor.Phone,
            Email = vendor.Email,
            PaymentTermDays = vendor.PaymentTermDays,
            BankAccount = BankAccountInput.From(vendor.BankAccount),
            PriceTolerancePercent = vendor.PriceTolerancePercent,
            IsActive = vendor.IsActive,
            Documents = [.. vendor.Documents],
            Attachments = await support.AttachmentsAsync(vendor.Documents, cancellationToken),
            CanSave = await support.CanAsync(MenuCode, MenuRights.Edit)
        });
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        VendorFormViewModel model,
        [FromServices] ICommandHandler<UpdateVendorCommand> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid && model.Id is Guid id)
        {
            Result result = await handler.Handle(
                new UpdateVendorCommand(
                    id, model.Name, model.TaxIdentity.ToRequest(), model.Address, model.Phone, model.Email,
                    model.PaymentTermDays!.Value, model.BankAccount.ToRequest(), model.IsActive == true,
                    model.PriceTolerancePercent ?? 0, model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Vendor {model.Code} has been saved.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        model.Attachments = await support.AttachmentsAsync(model.Documents, cancellationToken);
        return View("Form", model);
    }
}
