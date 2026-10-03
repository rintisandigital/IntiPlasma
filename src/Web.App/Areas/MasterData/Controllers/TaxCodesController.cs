using Application.Abstractions.Messaging;
using Application.TaxCodes;
using Application.TaxCodes.Create;
using Application.TaxCodes.Get;
using Application.TaxCodes.Update;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.MasterData.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Areas.MasterData.Controllers;

/// <summary>
/// PPN/PPh codes with dated rates (tariffs are data, not code — keputusan #5). VAT "DPP nilai lain" is entered as
/// an effective 11% with ratio 1 (keputusan #11).
/// </summary>
[Area("MasterData")]
[MenuAccess(MenuCodes.MasterTaxCodes)]
public sealed class TaxCodesController(
    PageSupport support,
    DisplayFormatter formatter,
    IQueryHandler<GetTaxCodesQuery, IReadOnlyList<TaxCodeResponse>> taxCodesQuery) : AppController
{
    private const string MenuCode = MenuCodes.MasterTaxCodes;

    [HttpGet]
    public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken)
    {
        IReadOnlyList<TaxCodeResponse> taxCodes = await ListAsync(search, cancellationToken);

        return View(new ListViewModel<TaxCodeResponse>
        {
            Rows = new PagedList<TaxCodeResponse>(taxCodes, 1, Math.Max(taxCodes.Count, 1), taxCodes.Count),
            Search = search
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(string? format, string? search, CancellationToken cancellationToken)
    {
        DateOnly today = formatter.Today();
        ExportColumn<TaxCodeResponse>[] columns =
        [
            new("Code", t => t.Code, Width: 1),
            new("Name", t => t.Name, Width: 3),
            new("Type", t => t.Type, Width: 1),
            new("Treatment / Article", t => t.VatTreatment ?? t.IncomeTaxArticle, Width: 1.5f),
            new("Current rate %", t => CurrentRate(t, today)?.RatePercent, ExportFormat.Percent, 1),
            new("Tax base ratio", t => CurrentRate(t, today)?.TaxBaseRatio, ExportFormat.Number, 1),
            new("Active", t => t.IsActive, ExportFormat.Boolean, 1)
        ];

        return await support.ExportAsync(format, "Tax Codes", "tax-codes",
            string.IsNullOrWhiteSpace(search) ? [] : [$"Search: {search}"], columns, await ListAsync(search, cancellationToken));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public IActionResult Create() =>
        View("Form", new TaxCodeFormViewModel { Rates = [new TaxRateInput { EffectiveFrom = formatter.Today() }] });

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        TaxCodeFormViewModel model,
        [FromServices] ICommandHandler<CreateTaxCodeCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(
                new CreateTaxCodeCommand(model.Code, model.Name, model.Type!.Value, model.VatTreatment, model.IncomeTaxArticle, Rates(model)),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Tax code {model.Code} has been created.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        return View("Form", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        TaxCodeResponse? taxCode = (await ListAsync(null, cancellationToken)).FirstOrDefault(t => t.Id == id);

        if (taxCode is null)
        {
            return NotFound();
        }

        return View("Form", new TaxCodeFormViewModel
        {
            Id = taxCode.Id,
            Code = taxCode.Code,
            Name = taxCode.Name,
            Type = Enum.Parse<Domain.MasterData.TaxCodes.TaxType>(taxCode.Type),
            VatTreatment = taxCode.VatTreatment is null ? null : Enum.Parse<Domain.MasterData.TaxCodes.VatTreatment>(taxCode.VatTreatment),
            IncomeTaxArticle = taxCode.IncomeTaxArticle is null ? null : Enum.Parse<Domain.MasterData.TaxCodes.IncomeTaxArticle>(taxCode.IncomeTaxArticle),
            IsActive = taxCode.IsActive,
            Rates = [.. taxCode.Rates.Select(r => new TaxRateInput { EffectiveFrom = r.EffectiveFrom, RatePercent = r.RatePercent, TaxBaseRatio = r.TaxBaseRatio })],
            CanSave = await support.CanAsync(MenuCode, MenuRights.Edit)
        });
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        TaxCodeFormViewModel model,
        [FromServices] ICommandHandler<UpdateTaxCodeCommand> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid && model.Id is Guid id)
        {
            Result result = await handler.Handle(
                new UpdateTaxCodeCommand(id, model.Name, model.IsActive == true, Rates(model)), cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Tax code {model.Code} has been saved.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        return View("Form", model);
    }

    private static List<TaxRateRequest> Rates(TaxCodeFormViewModel model) =>
        [.. model.Rates.Select(r => new TaxRateRequest(r.EffectiveFrom!.Value, r.RatePercent!.Value, r.TaxBaseRatio!.Value))];

    private static TaxRateResponse? CurrentRate(TaxCodeResponse taxCode, DateOnly today) =>
        taxCode.Rates.Where(r => r.EffectiveFrom <= today).MaxBy(r => r.EffectiveFrom);

    private async Task<IReadOnlyList<TaxCodeResponse>> ListAsync(string? search, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<TaxCodeResponse>> result = await taxCodesQuery.Handle(new GetTaxCodesQuery(), cancellationToken);
        IReadOnlyList<TaxCodeResponse> taxCodes = result.IsSuccess ? result.Value : [];

        return string.IsNullOrWhiteSpace(search)
            ? taxCodes
            : [.. taxCodes.Where(t => t.Code.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                     t.Name.Contains(search, StringComparison.OrdinalIgnoreCase))];
    }
}
