using Application.Costing;
using Application.Cycles;
using Application.Production;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;
using CyclePerformance = Domain.Partnership.Cycles.CyclePerformance;

namespace Web.App.Areas.Production.Documents;

/// <summary>
/// Ringkasan siklus: identity of the cycle, population, performance (closing figures once closed), harvests and
/// cost of goods. Before closing the figures are interim.
/// </summary>
public static class CycleSummaryPdf
{
    private static readonly string[] HarvestHeaders = ["Date", "Age (days)", "Birds", "Weight (kg)", "Avg (kg)", "Notes"];
    private static readonly string[] Signers = ["Prepared by", "Production", "Finance"];

    public static void Compose(
        IContainer container,
        CycleResponse cycle,
        CyclePerformanceResponse? performance,
        CycleCostResponse? cost,
        DisplayFormatter fmt)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(cycle);
        ArgumentNullException.ThrowIfNull(fmt);

        CyclePerformance? figures = performance?.Closing ?? performance?.Current;

        container.Column(column =>
        {
            column.Spacing(10);

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(105);
                    c.RelativeColumn();
                    c.ConstantColumn(105);
                    c.RelativeColumn();
                });

                void Field(string label, string value)
                {
                    table.Cell().PaddingVertical(1).Text(label).SemiBold();
                    table.Cell().PaddingVertical(1).Text(value);
                }

                Field("Cycle", cycle.Number);
                Field("Status", cycle.Status);
                Field("Farmer", $"{cycle.FarmerName} ({cycle.FarmerType})");
                Field("Farm", $"{cycle.CoopCode} — {cycle.CoopName}");
                Field("Branch", cycle.BranchCode);
                Field("Contract", cycle.ContractCode ?? "—");
                Field("Chick-in", cycle.ChickInDate is { } chickIn ? fmt.Date(chickIn) : $"planned {fmt.Date(cycle.PlannedChickInDate)}");
                Field("Closed", cycle.ClosedDate is { } closed ? fmt.Date(closed) : "—");
            });

            if (figures is not null)
            {
                column.Item().Text(performance?.Closing is null ? "Performance (interim)" : "Closing performance").Bold().FontSize(10);
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            c.RelativeColumn();
                        }
                    });

                    void Metric(string label, string value)
                    {
                        table.Cell().Element(PdfLayout.Cell).Text(label).SemiBold();
                        table.Cell().Element(PdfLayout.Cell).AlignRight().Text(value);
                    }

                    Metric("Initial population", fmt.Number(figures.InitialPopulation));
                    Metric("Mortality / culling", $"{fmt.Number(figures.Mortality)} / {fmt.Number(figures.Culling)}");
                    Metric("Depletion", $"{fmt.Number(figures.DepletionPercent, 2)} %");
                    Metric("Harvested birds", fmt.Number(figures.HarvestedBirds));
                    Metric("Harvested weight", $"{fmt.Number(figures.HarvestedWeightKg, 2)} kg");
                    Metric("Feed used", $"{fmt.Number(figures.FeedKg, 2)} kg");
                    Metric("Average weight", figures.AverageWeightKg is { } bw ? $"{fmt.Number(bw, 3)} kg" : "—");
                    Metric("FCR", figures.Fcr is { } fcr ? fmt.Number(fcr, 3) : "—");
                    Metric("Age", figures.AgeDays is { } age ? $"{fmt.Number(age, 1)} days" : "—");
                    Metric("ADG", figures.AdgGram is { } adg ? $"{fmt.Number(adg, 1)} g" : "—");
                    Metric("IP", figures.Ip is { } ip ? fmt.Number(ip, 0) : "—");
                    Metric("Population left", fmt.Number(figures.Population));
                });
            }

            if (performance is { Harvests.Count: > 0 })
            {
                column.Item().Text("Harvests").Bold().FontSize(10);
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1.1f);
                        c.RelativeColumn(0.8f);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(0.9f);
                        c.RelativeColumn(2.5f);
                    });
                    table.Header(h =>
                    {
                        foreach (string title in HarvestHeaders)
                        {
                            h.Cell().Element(PdfLayout.HeaderCell).Text(title).Bold();
                        }
                    });

                    foreach (HarvestResponse harvest in performance.Harvests)
                    {
                        table.Cell().Element(PdfLayout.Cell).Text(fmt.Date(harvest.Date));
                        table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(harvest.AgeDays));
                        table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(harvest.Birds));
                        table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(harvest.WeightKg, 2));
                        table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(harvest.AverageWeightKg, 3));
                        table.Cell().Element(PdfLayout.Cell).Text(harvest.Notes ?? string.Empty);
                    }
                });
            }

            if (cost is not null)
            {
                column.Item().Text(cost.IsFinal ? "Cost of goods (final)" : "Cost of goods (running estimate)").Bold().FontSize(10);
                column.Item().Width(330).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn();
                        c.RelativeColumn();
                    });

                    void Line(string label, decimal? value, string suffix = "")
                    {
                        table.Cell().Element(PdfLayout.Cell).Text(label);
                        table.Cell().Element(PdfLayout.Cell).AlignRight().Text(value is { } v ? $"Rp {fmt.Number(v)}{suffix}" : "—");
                    }

                    Line("DOC", cost.DocCost);
                    Line("Feed", cost.FeedCost);
                    Line("OVK", cost.OvkCost);
                    Line("Total sapronak cost", cost.TotalCost);
                    Line("Cost per kg live weight", cost.CostPerKg);
                    Line("Cost per bird", cost.CostPerBird);
                    if (cost.PlasmaIncome is not null)
                    {
                        Line("Plasma income", cost.PlasmaIncome);
                        Line("Total cost incl. plasma", cost.TotalCostWithPlasma);
                    }
                });
            }

            column.Item().PaddingTop(25).Row(row =>
            {
                foreach (string signer in Signers)
                {
                    row.RelativeItem().AlignCenter().Column(sign =>
                    {
                        sign.Item().AlignCenter().Text(signer).SemiBold();
                        sign.Item().Height(50);
                        sign.Item().AlignCenter().Text("(____________________)");
                    });
                }
            });
        });
    }
}
