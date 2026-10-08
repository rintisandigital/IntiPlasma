using Application.Costing;
using Domain.Partnership.Cycles;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Web.App.Areas.Costing.Models;
using Web.App.Areas.MasterData.Models;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Areas.Costing.Documents;

/// <summary>
/// Printed plasma settlement (slip perhitungan hasil, PLAN-WEBAPP §5.3): cycle and closing performance, the
/// calculation lines per type, income tax, debt deduction and the amount paid — or the loss booked as plasma debt —
/// in words in Indonesian (W-17).
/// </summary>
public static class CostingDocumentPdf
{
    private static readonly string[] LineHeaders = ["#", "Description", "Quantity", "Unit price", "Amount"];

    public static void Settlement(IContainer container, PlasmaSettlementResponse settlement, DisplayFormatter fmt)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(fmt);

        bool loss = settlement.GrossIncome < 0;
        DocumentPdf.Compose(container, column =>
        {
            column.Item().Element(c => DocumentPdf.Fields(c,
            [
                ("Plasma", $"{settlement.FarmerName} ({settlement.FarmerCode})"),
                ("Settlement", settlement.Number),
                ("Farm", $"{settlement.CoopCode} — {settlement.CoopName}"),
                ("Date", fmt.Date(settlement.SettlementDate)),
                ("Cycle", settlement.CycleNumber),
                ("Contract", $"{settlement.ContractCode} ({SettlementLines.Scheme(settlement.Scheme)})"),
                ("Chick-in", settlement.ChickInDate is { } chickIn ? fmt.Date(chickIn) : "—"),
                ("Closed", settlement.ClosedDate is { } closed ? fmt.Date(closed) : "—"),
                ("Branch", settlement.BranchCode),
                ("Status", settlement.Status == "Cancelled" ? $"Cancelled — {settlement.CancellationReason}" : EnumOptions.Label(settlement.Status))
            ]));

            if (settlement.Performance is { } performance)
            {
                column.Item().Text("Closing performance").Bold().FontSize(10);
                column.Item().Element(c => DocumentPdf.Fields(c, Performance(performance, fmt)));
            }

            column.Item().Text("Calculation").Bold().FontSize(10);
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(22);
                    c.RelativeColumn(3.2f);
                    c.RelativeColumn(1.1f);
                    c.RelativeColumn(1.2f);
                    c.RelativeColumn(1.4f);
                });
                DocumentPdf.Header(table, LineHeaders);

                foreach (IGrouping<string, PlasmaSettlementLineResponse> group in
                         (settlement.Lines ?? []).GroupBy(l => l.Type).OrderBy(g => SettlementLines.Order(g.Key)))
                {
                    table.Cell().ColumnSpan(5).Element(PdfLayout.Cell).Text(SettlementLines.Title(group.Key)).SemiBold();
                    foreach (PlasmaSettlementLineResponse line in group)
                    {
                        table.Cell().Element(PdfLayout.Cell).Text(line.LineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        table.Cell().Element(PdfLayout.Cell).Text(line.Description);
                        table.Cell().Element(PdfLayout.Cell).AlignRight().Text(line.Quantity is { } q ? fmt.Number(q, 3) : string.Empty);
                        table.Cell().Element(PdfLayout.Cell).AlignRight().Text(line.UnitPrice is { } p ? fmt.Number(p) : string.Empty);
                        table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(line.Amount));
                    }
                }
            });

            List<(string, decimal, bool)> totals = [("Gross income", settlement.GrossIncome, true)];
            if (loss)
            {
                totals.Add(("Loss (plasma debt)", settlement.Deficit, true));
            }
            else
            {
                totals.Add(($"Income tax{(settlement.IncomeTaxCode is { } tax ? $" {tax} ({fmt.Number(settlement.IncomeTaxRatePercent)}%)" : string.Empty)}",
                    -settlement.IncomeTaxAmount, false));
                totals.Add(("Debt deduction", -settlement.DebtDeduction, false));
                totals.Add(("Net payable", settlement.NetPayable, true));
            }

            DocumentPdf.Totals(column, totals);
            DocumentPdf.InWords(column, loss ? settlement.Deficit : settlement.NetPayable);
            if (loss)
            {
                column.Item().Text("The loss is not paid; it is booked as the plasma's debt and may be deducted from a later settlement.").Italic();
            }

            DocumentPdf.Notes(column, settlement.Notes);
            DocumentPdf.Signatures(column, ["Prepared by", "Approved by", "Plasma"]);
        });
    }

    private static List<(string, string)> Performance(CyclePerformance performance, DisplayFormatter fmt) =>
    [
        ("Initial population", fmt.Number(performance.InitialPopulation)),
        ("Harvested", $"{fmt.Number(performance.HarvestedBirds)} birds / {fmt.Number(performance.HarvestedWeightKg)} kg"),
        ("Depletion", $"{fmt.Number(performance.DepletionPercent)} %"),
        ("Average weight", performance.AverageWeightKg is { } bw ? $"{fmt.Number(bw, 3)} kg" : "—"),
        ("FCR", performance.Fcr is { } fcr ? fmt.Number(fcr, 3) : "—"),
        ("IP", performance.Ip is { } ip ? fmt.Number(ip, 0) : "—"),
        ("Age", performance.AgeDays is { } age ? $"{fmt.Number(age, 1)} days" : "—"),
        ("Feed used", $"{fmt.Number(performance.FeedKg)} kg")
    ];
}
