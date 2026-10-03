using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Items;
using Application.Items.Create;
using Application.Items.Get;
using Application.Items.GetById;
using Application.Items.Update;
using Application.TaxCodes.Get;
using Application.Uoms.Get;
using Domain.Access;
using Domain.MasterData.Items;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;
using Web.App.Areas.MasterData.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;

namespace Web.App.Areas.MasterData.Controllers;

/// <summary>
/// Sapronak and products with their base unit and unit conversions (e.g. 1 SAK = 50 KG).
/// </summary>
[Area("MasterData")]
[MenuAccess(MenuCodes.MasterItems)]
public sealed class ItemsController(
    PageSupport support,
    IQueryHandler<GetItemsQuery, PagedList<ItemResponse>> itemsQuery,
    IQueryHandler<GetUomsQuery, IReadOnlyList<UomResponse>> uomsQuery,
    IQueryHandler<GetTaxCodesQuery, IReadOnlyList<TaxCodeResponse>> taxCodesQuery) : AppController
{
    private const string MenuCode = MenuCodes.MasterItems;

    private static readonly ExportColumn<ItemResponse>[] Columns =
    [
        new("Code", i => i.Code, Width: 1.2f),
        new("Name", i => i.Name, Width: 3),
        new("Category", i => i.Category, Width: 1),
        new("Base unit", i => i.BaseUomCode, Width: 1),
        new("Tax code", i => i.TaxCode, Width: 1),
        new("Active", i => i.IsActive, ExportFormat.Boolean, 0.8f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(string? search, ItemCategory? category, int? page, CancellationToken cancellationToken)
    {
        Result<PagedList<ItemResponse>> result = await itemsQuery.Handle(
            new GetItemsQuery(new PageRequest(page, PageRequest.DefaultPageSize, search), category), cancellationToken);

        return View(new ListViewModel<ItemResponse>
        {
            Rows = result.Value,
            Search = search,
            Filters = new Dictionary<string, string?> { ["category"] = category?.ToString() },
            FilterOptions = new Dictionary<string, IReadOnlyList<SelectListItem>> { ["category"] = EnumOptions.For(category) }
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public Task<IActionResult> Export(string? format, string? search, ItemCategory? category, CancellationToken cancellationToken)
    {
        List<string> filters = [];
        if (category is not null)
        {
            filters.Add($"Category: {category}");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            filters.Add($"Search: {search}");
        }

        return support.ExportAsync(format, "Items", "items", filters, Columns,
            (paging, ct) => itemsQuery.Handle(new GetItemsQuery(paging, category), ct), search, cancellationToken);
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken) =>
        View("Form", await WithOptionsAsync(new ItemFormViewModel(), cancellationToken));

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        ItemFormViewModel model,
        [FromServices] ICommandHandler<CreateItemCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(
                new CreateItemCommand(model.Code, model.Name, model.Category!.Value, model.BaseUomId!.Value, model.TaxCodeId, Conversions(model)),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Item {model.Code} has been created.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(
        Guid id,
        [FromServices] IQueryHandler<GetItemByIdQuery, ItemResponse> query,
        CancellationToken cancellationToken)
    {
        Result<ItemResponse> result = await query.Handle(new GetItemByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return NotFound();
        }

        ItemResponse item = result.Value;
        var model = new ItemFormViewModel
        {
            Id = item.Id,
            Code = item.Code,
            Name = item.Name,
            Category = Enum.Parse<ItemCategory>(item.Category),
            BaseUomId = item.BaseUomId,
            TaxCodeId = item.TaxCodeId,
            IsActive = item.IsActive,
            Conversions = [.. item.Conversions.Select(c => new ItemConversionInput { UomId = c.UomId, Factor = c.Factor })],
            CanSave = await support.CanAsync(MenuCode, MenuRights.Edit)
        };

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        ItemFormViewModel model,
        [FromServices] ICommandHandler<UpdateItemCommand> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid && model.Id is Guid id)
        {
            Result result = await handler.Handle(
                new UpdateItemCommand(id, model.Name, model.TaxCodeId, model.IsActive == true, Conversions(model)), cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Item {model.Code} has been saved.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    private static List<ItemUomConversionRequest> Conversions(ItemFormViewModel model) =>
        [.. model.Conversions.Select(c => new ItemUomConversionRequest(c.UomId!.Value, c.Factor!.Value))];

    private async Task<ItemFormViewModel> WithOptionsAsync(ItemFormViewModel model, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<UomResponse>> uoms = await uomsQuery.Handle(new GetUomsQuery(), cancellationToken);
        Result<IReadOnlyList<TaxCodeResponse>> taxCodes = await taxCodesQuery.Handle(new GetTaxCodesQuery(), cancellationToken);

        model.Uoms = uoms.IsSuccess ? [.. uoms.Value.Select(u => new SelectListItem($"{u.Code} — {u.Name}", u.Id.ToString()))] : [];
        model.TaxCodes = taxCodes.IsSuccess
            ? [.. taxCodes.Value.Where(t => t.IsActive || t.Id == model.TaxCodeId).Select(t => new SelectListItem($"{t.Code} — {t.Name}", t.Id.ToString()))]
            : [];

        return model;
    }
}
