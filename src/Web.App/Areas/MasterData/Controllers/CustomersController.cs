using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Customers;
using Application.Customers.Create;
using Application.Customers.Get;
using Application.Customers.GetById;
using Application.Customers.Update;
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
/// Buyers of live birds: tax identity (NPWP 15/16 digits, NITKU 22 digits), payment term and credit limit
/// (0 = no credit; checked when a sales order is approved).
/// </summary>
[Area("MasterData")]
[MenuAccess(MenuCodes.MasterCustomers)]
public sealed class CustomersController(
    PageSupport support,
    IQueryHandler<GetCustomersQuery, PagedList<CustomerResponse>> customersQuery) : AppController
{
    private const string MenuCode = MenuCodes.MasterCustomers;

    private static readonly ExportColumn<CustomerResponse>[] Columns =
    [
        new("Code", c => c.Code, Width: 1.2f),
        new("Name", c => c.Name, Width: 3),
        new("NPWP", c => c.TaxIdentity.Npwp, Width: 1.8f),
        new("NITKU", c => c.TaxIdentity.Nitku, Width: 2),
        new("PKP", c => c.TaxIdentity.IsPkp, ExportFormat.Boolean, 0.6f),
        new("Phone", c => c.Phone, Width: 1.3f),
        new("Term (days)", c => c.PaymentTermDays, ExportFormat.WholeNumber, 0.8f),
        new("Credit limit", c => c.CreditLimit, ExportFormat.Money, 1.5f),
        new("Active", c => c.IsActive, ExportFormat.Boolean, 0.6f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int? page, CancellationToken cancellationToken)
    {
        Result<PagedList<CustomerResponse>> result = await customersQuery.Handle(
            new GetCustomersQuery(new PageRequest(page, PageRequest.DefaultPageSize, search)), cancellationToken);

        return View(new ListViewModel<CustomerResponse> { Rows = result.Value, Search = search });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public Task<IActionResult> Export(string? format, string? search, CancellationToken cancellationToken) =>
        support.ExportAsync(format, "Customers", "customers", string.IsNullOrWhiteSpace(search) ? [] : [$"Search: {search}"], Columns,
            (paging, ct) => customersQuery.Handle(new GetCustomersQuery(paging), ct), search, cancellationToken);

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public IActionResult Create() => View("Form", new CustomerFormViewModel { PaymentTermDays = 7, CreditLimit = 0 });

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        CustomerFormViewModel model,
        [FromServices] ICommandHandler<CreateCustomerCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(
                new CreateCustomerCommand(
                    model.Code, model.Name, model.TaxIdentity.ToRequest(), model.Address, model.Phone, model.Email,
                    model.PaymentTermDays!.Value, model.CreditLimit!.Value, model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Customer {model.Code} has been created.");
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
        [FromServices] IQueryHandler<GetCustomerByIdQuery, CustomerResponse> query,
        CancellationToken cancellationToken)
    {
        Result<CustomerResponse> result = await query.Handle(new GetCustomerByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return NotFound();
        }

        CustomerResponse customer = result.Value;

        return View("Form", new CustomerFormViewModel
        {
            Id = customer.Id,
            Code = customer.Code,
            Name = customer.Name,
            TaxIdentity = TaxIdentityInput.From(customer.TaxIdentity),
            Address = customer.Address,
            Phone = customer.Phone,
            Email = customer.Email,
            PaymentTermDays = customer.PaymentTermDays,
            CreditLimit = customer.CreditLimit,
            IsActive = customer.IsActive,
            Documents = [.. customer.Documents],
            Attachments = await support.AttachmentsAsync(customer.Documents, cancellationToken),
            CanSave = await support.CanAsync(MenuCode, MenuRights.Edit)
        });
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        CustomerFormViewModel model,
        [FromServices] ICommandHandler<UpdateCustomerCommand> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid && model.Id is Guid id)
        {
            Result result = await handler.Handle(
                new UpdateCustomerCommand(
                    id, model.Name, model.TaxIdentity.ToRequest(), model.Address, model.Phone, model.Email,
                    model.PaymentTermDays!.Value, model.CreditLimit!.Value, model.IsActive == true, model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Customer {model.Code} has been saved.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        model.Attachments = await support.AttachmentsAsync(model.Documents, cancellationToken);
        return View("Form", model);
    }
}
