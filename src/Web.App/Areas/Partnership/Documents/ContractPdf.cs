using Application.Contracts;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Web.App.Areas.MasterData.Models;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Areas.Partnership.Documents;

/// <summary>
/// Printed partnership contract (PLAN-WEBAPP §5.3): parties/terms, sapronak contract prices, guaranteed live bird
/// prices, incentives, notes and signature blocks.
/// </summary>
public static class ContractPdf
{
    private static readonly string[] IncentiveHeaders = ["Name", "Kind", "Metric", "Range", "Amount (Rp)", "Basis"];
    private static readonly string[] Parties = ["Inti", "Plasma"];

    public static void Compose(IContainer container, ContractResponse contract, DisplayFormatter fmt)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(fmt);

        container.Column(column =>
        {
            column.Spacing(10);

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(130);
                    c.RelativeColumn();
                });

                void Row(string label, string? value)
                {
                    table.Cell().PaddingVertical(2).Text(label).SemiBold();
                    table.Cell().PaddingVertical(2).Text(value ?? "—");
                }

                Row("Contract", $"{contract.Code} — {contract.Name}");
                Row("Branch", contract.BranchCode);
                Row("Scheme", EnumOptions.Label(contract.Scheme));
                Row("Status", contract.Status);
                Row("Valid", $"{fmt.Date(contract.ValidFrom)} – {(contract.ValidTo is { } to ? fmt.Date(to) : "open-ended")}");
                if (contract.PlasmaProfitSharePercent is { } share)
                {
                    Row("Plasma profit share", $"{fmt.Number(share)}% of (net sales − cycle cost); losses are borne by the inti");
                }

                Row("Income tax (PPh)", contract.IncomeTaxCode);
            });

            if (contract.InputPrices is { Count: > 0 } inputPrices)
            {
                column.Item().Text("Sapronak contract prices").Bold().FontSize(10);
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(3);
                        c.RelativeColumn(0.8f);
                        c.RelativeColumn(1.5f);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Element(PdfLayout.HeaderCell).Text("Code").Bold();
                        h.Cell().Element(PdfLayout.HeaderCell).Text("Item").Bold();
                        h.Cell().Element(PdfLayout.HeaderCell).Text("Unit").Bold();
                        h.Cell().Element(PdfLayout.HeaderCell).AlignRight().Text("Price (Rp)").Bold();
                    });
                    foreach (ContractResponse.InputPriceResponse price in inputPrices)
                    {
                        table.Cell().Element(PdfLayout.Cell).Text(price.ItemCode);
                        table.Cell().Element(PdfLayout.Cell).Text(price.ItemName);
                        table.Cell().Element(PdfLayout.Cell).Text(price.UomCode);
                        table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(price.Price));
                    }
                });
            }

            if (contract.LiveBirdPrices is { Count: > 0 } birdPrices)
            {
                column.Item().Text("Guaranteed live bird prices (per harvest, by average weight)").Bold().FontSize(10);
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn();
                        c.RelativeColumn();
                        c.RelativeColumn();
                    });
                    table.Header(h =>
                    {
                        h.Cell().Element(PdfLayout.HeaderCell).AlignRight().Text("From (kg)").Bold();
                        h.Cell().Element(PdfLayout.HeaderCell).AlignRight().Text("To (kg)").Bold();
                        h.Cell().Element(PdfLayout.HeaderCell).AlignRight().Text("Price per kg (Rp)").Bold();
                    });
                    foreach (ContractResponse.LiveBirdPriceResponse price in birdPrices)
                    {
                        table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(price.MinWeightKg, 3));
                        table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(price.MaxWeightKg, 3));
                        table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(price.PricePerKg));
                    }
                });
            }

            if (contract.Incentives is { Count: > 0 } incentives)
            {
                column.Item().Text("Bonuses and deductions").Bold().FontSize(10);
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2.5f);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(1.5f);
                        c.RelativeColumn(1.3f);
                        c.RelativeColumn(1);
                    });
                    table.Header(h =>
                    {
                        foreach (string title in IncentiveHeaders)
                        {
                            h.Cell().Element(PdfLayout.HeaderCell).Text(title).Bold();
                        }
                    });
                    foreach (ContractResponse.IncentiveResponse incentive in incentives)
                    {
                        table.Cell().Element(PdfLayout.Cell).Text(incentive.Name);
                        table.Cell().Element(PdfLayout.Cell).Text(incentive.Kind);
                        table.Cell().Element(PdfLayout.Cell).Text(EnumOptions.Label(incentive.Metric));
                        table.Cell().Element(PdfLayout.Cell).Text(Range(incentive, fmt));
                        table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(incentive.Amount));
                        table.Cell().Element(PdfLayout.Cell).Text(EnumOptions.Label(incentive.Basis));
                    }
                });
            }

            if (!string.IsNullOrWhiteSpace(contract.Notes))
            {
                column.Item().Text("Notes").Bold().FontSize(10);
                column.Item().Text(contract.Notes);
            }

            column.Item().PaddingTop(30).Row(row =>
            {
                foreach (string party in Parties)
                {
                    row.RelativeItem().AlignCenter().Column(sign =>
                    {
                        sign.Item().AlignCenter().Text(party).SemiBold();
                        sign.Item().Height(55);
                        sign.Item().AlignCenter().Text("(____________________)");
                    });
                }
            });
        });
    }

    private static string Range(ContractResponse.IncentiveResponse incentive, DisplayFormatter fmt) =>
        (incentive.RangeFrom, incentive.RangeTo) switch
        {
            (null, null) => "—",
            ({ } from, null) => $"≥ {fmt.Number(from, 3)}",
            (null, { } to) => $"≤ {fmt.Number(to, 3)}",
            ({ } from, { } to) => $"{fmt.Number(from, 3)} – {fmt.Number(to, 3)}"
        };
}
