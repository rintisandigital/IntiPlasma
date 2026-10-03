using Application.Procurement;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Web.App.Areas.MasterData.Models;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Areas.Procurement.Documents;

/// <summary>
/// Printed purchase order (PLAN-WEBAPP §5.3): vendor and delivery details, lines with VAT code, totals with the VAT
/// estimate, notes and signature blocks.
/// </summary>
public static class PurchaseOrderPdf
{
    private static readonly string[] LineHeaders = ["#", "Item", "Qty", "Unit", "Unit price (Rp)", "VAT", "Amount (Rp)"];
    private static readonly string[] Signers = ["Prepared by", "Approved by", "Vendor"];

    public static void Compose(
        IContainer container,
        PurchaseOrderResponse order,
        IReadOnlyDictionary<int, string> lineTaxCodes,
        decimal vat,
        DisplayFormatter fmt)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(lineTaxCodes);
        ArgumentNullException.ThrowIfNull(fmt);

        container.Column(column =>
        {
            column.Spacing(10);

            column.Item().Row(row =>
            {
                row.RelativeItem().Column(vendor =>
                {
                    vendor.Item().Text("Vendor").SemiBold();
                    vendor.Item().Text($"{order.VendorName} ({order.VendorCode})");
                });
                row.RelativeItem().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(95);
                        c.RelativeColumn();
                    });

                    void Field(string label, string value)
                    {
                        table.Cell().PaddingVertical(1).Text(label).SemiBold();
                        table.Cell().PaddingVertical(1).Text(value);
                    }

                    Field("PO number", order.Number);
                    Field("Order date", fmt.Date(order.OrderDate));
                    Field("Expected delivery", order.ExpectedDate is { } expected ? fmt.Date(expected) : "—");
                    Field("Branch", order.BranchCode);
                    Field("Status", EnumOptions.Label(order.Status));
                });
            });

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(22);
                    c.RelativeColumn(4);
                    c.RelativeColumn(1.2f);
                    c.RelativeColumn(0.8f);
                    c.RelativeColumn(1.6f);
                    c.RelativeColumn(0.9f);
                    c.RelativeColumn(1.8f);
                });
                table.Header(h =>
                {
                    foreach (string title in LineHeaders)
                    {
                        h.Cell().Element(PdfLayout.HeaderCell).Text(title).Bold();
                    }
                });

                foreach (PurchaseOrderLineResponse line in order.Lines ?? [])
                {
                    table.Cell().Element(PdfLayout.Cell).Text(line.LineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    table.Cell().Element(PdfLayout.Cell).Text($"{line.ItemCode} — {line.ItemName}");
                    table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(line.Quantity, 3));
                    table.Cell().Element(PdfLayout.Cell).Text(line.UomCode);
                    table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(line.UnitPrice));
                    table.Cell().Element(PdfLayout.Cell).Text(lineTaxCodes.GetValueOrDefault(line.LineNumber, "—"));
                    table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(line.Amount));
                }
            });

            column.Item().AlignRight().Width(230).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn();
                    c.RelativeColumn();
                });

                void Total(string label, decimal value, bool bold = false)
                {
                    TextBlockDescriptor l = table.Cell().PaddingVertical(2).Text(label);
                    TextBlockDescriptor v = table.Cell().PaddingVertical(2).AlignRight().Text($"Rp {fmt.Number(value)}");
                    if (bold)
                    {
                        l.Bold();
                        v.Bold();
                    }
                }

                Total("Subtotal", order.Subtotal);
                Total("VAT (estimate)", vat);
                Total("Total", order.Subtotal + vat, bold: true);
            });

            if (!string.IsNullOrWhiteSpace(order.Notes))
            {
                column.Item().Text("Notes").Bold().FontSize(10);
                column.Item().Text(order.Notes);
            }

            column.Item().Text("VAT is an estimate from the VAT codes at the order date; the vendor invoice determines the final amount.")
                .FontSize(8).Italic();

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
