using Application.Abstractions.Messaging;
using Application.Finance.FiscalPeriods;
using Application.Finance.Tax;
using Application.Monitoring;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed class ClosingAndTaxEndpoints : IEndpoint
{
    public sealed record RetryRequest(Guid? EventId);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("finance/fiscal-periods/{fiscalPeriodId:guid}/checklist", async (
            Guid fiscalPeriodId,
            IQueryHandler<GetPeriodClosingChecklistQuery, PeriodClosingChecklist> handler,
            CancellationToken cancellationToken) =>
        {
            Result<PeriodClosingChecklist> result = await handler.Handle(new GetPeriodClosingChecklistQuery(fiscalPeriodId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.FiscalPeriods)
        .HasPermission(Permissions.FinanceSetupRead);

        MapTax(app.MapGroup("finance/tax").WithTags(Tags.TaxReports));
        MapMonitoring(app.MapGroup("system/failed-events").WithTags(Tags.Monitoring));
    }

    private static void MapTax(RouteGroupBuilder group)
    {
        group.MapGet("vat", async (
            int year,
            int month,
            Guid? branchId,
            IQueryHandler<GetVatRecapQuery, VatRecapResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<VatRecapResponse> result = await handler.Handle(new GetVatRecapQuery(year, month, branchId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.TaxReportsRead);

        // section: output (PPN keluaran), output-returns (retur/nota kredit), input (PPN masukan)
        group.MapGet("vat/export", async (
            int year,
            int month,
            string section,
            Guid? branchId,
            IQueryHandler<GetVatRecapQuery, VatRecapResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<VatRecapResponse> result = await handler.Handle(new GetVatRecapQuery(year, month, branchId), cancellationToken);
            if (result.IsFailure)
            {
                return CustomResults.Problem(result);
            }

            VatRecapResponse r = result.Value;
            string period = $"{year}{month:D2}";

            return section switch
            {
                "output" => Csv.File(
                    $"ppn-keluaran-{period}.csv",
                    r.Output,
                    ("nomor_invoice", l => l.InvoiceNumber), ("tanggal", l => l.InvoiceDate), ("cabang", l => l.BranchCode),
                    ("kode_customer", l => l.CustomerCode), ("nama_customer", l => l.CustomerName), ("npwp", l => l.Npwp),
                    ("nitku", l => l.Nitku), ("pkp", l => l.IsPkp), ("kode_pajak", l => l.TaxCodes), ("dpp", l => l.Dpp),
                    ("dpp_nilai_lain", l => l.DppNilaiLain), ("ppn", l => l.Ppn)),
                "output-returns" => Csv.File(
                    $"retur-ppn-keluaran-{period}.csv",
                    r.OutputReturns,
                    ("nomor_nota_kredit", l => l.CreditNoteNumber), ("tanggal", l => l.Date), ("nomor_invoice", l => l.InvoiceNumber),
                    ("nama_customer", l => l.CustomerName), ("npwp", l => l.Npwp), ("dpp", l => l.Dpp), ("ppn", l => l.Ppn)),
                "input" => Csv.File(
                    $"ppn-masukan-{period}.csv",
                    r.Input,
                    ("nomor_internal", l => l.InvoiceNumber), ("nomor_invoice_vendor", l => l.VendorInvoiceNumber),
                    ("nomor_faktur_pajak", l => l.TaxInvoiceNumber), ("tanggal", l => l.InvoiceDate), ("cabang", l => l.BranchCode),
                    ("kode_vendor", l => l.VendorCode), ("nama_vendor", l => l.VendorName), ("npwp", l => l.Npwp),
                    ("nitku", l => l.Nitku), ("dpp", l => l.Dpp), ("dpp_nilai_lain", l => l.DppNilaiLain), ("ppn", l => l.Ppn)),
                _ => Results.Problem(
                    title: "TaxReports.UnknownSection",
                    detail: "section must be output, output-returns or input",
                    statusCode: StatusCodes.Status400BadRequest)
            };
        })
        .HasPermission(Permissions.TaxReportsRead);

        group.MapGet("withholding", async (
            int year,
            int month,
            Guid? branchId,
            IQueryHandler<GetWithholdingRecapQuery, WithholdingRecapResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<WithholdingRecapResponse> result = await handler.Handle(new GetWithholdingRecapQuery(year, month, branchId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.TaxReportsRead);

        group.MapGet("withholding/export", async (
            int year,
            int month,
            Guid? branchId,
            IQueryHandler<GetWithholdingRecapQuery, WithholdingRecapResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<WithholdingRecapResponse> result = await handler.Handle(new GetWithholdingRecapQuery(year, month, branchId), cancellationToken);
            if (result.IsFailure)
            {
                return CustomResults.Problem(result);
            }

            return Csv.File(
                $"pph-dipotong-{year}{month:D2}.csv",
                result.Value.Lines,
                ("sumber", l => l.Source), ("nomor_dokumen", l => l.DocumentNumber), ("tanggal", l => l.Date),
                ("cabang", l => l.BranchCode), ("kode_penerima", l => l.PayeeCode), ("nama_penerima", l => l.PayeeName),
                ("npwp", l => l.Npwp), ("nik", l => l.Nik), ("kode_pajak", l => l.TaxCode), ("pasal", l => l.Article),
                ("dasar_pengenaan", l => l.TaxBase), ("tarif_persen", l => l.RatePercent), ("pph", l => l.Amount));
        })
        .HasPermission(Permissions.TaxReportsRead);
    }

    private static void MapMonitoring(RouteGroupBuilder group)
    {
        group.MapGet("", async (
            IQueryHandler<GetFailedEventsQuery, IReadOnlyList<FailedEventResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<FailedEventResponse>> result = await handler.Handle(new GetFailedEventsQuery(), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.SystemOutbox);

        group.MapPost("retry", async (
            RetryRequest request,
            ICommandHandler<RetryFailedEventsCommand, int> handler,
            CancellationToken cancellationToken) =>
        {
            Result<int> result = await handler.Handle(new RetryFailedEventsCommand(request.EventId), cancellationToken);

            return result.Match(count => Results.Ok(new { Requeued = count }), CustomResults.Problem);
        })
        .HasPermission(Permissions.SystemOutbox);
    }
}
