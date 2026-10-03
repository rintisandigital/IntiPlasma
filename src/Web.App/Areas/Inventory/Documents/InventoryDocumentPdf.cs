using System.Globalization;
using Application.Inventory;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Areas.Inventory.Documents;

/// <summary>
/// Printed goods receipt (BPB), stock transfer or stock return: header fields, lines with quantity in the entered
/// and the base unit, value at cost, notes and signature blocks.
/// </summary>
public static class InventoryDocumentPdf
{
    private static readonly string[] LineHeaders = ["#", "Item", "Qty", "Unit", "Base qty", "Unit cost (Rp)", "Value (Rp)"];

    /// <param name="fields">Header fields (label, value) printed next to each other.</param>
    /// <param name="signers">Signature captions, e.g. "Received by", "Delivered by".</param>
    public static void Compose(
        IContainer container,
        InventoryDocumentResponse document,
        IReadOnlyList<(string Label, string Value)> fields,
        IReadOnlyList<string> signers,
        DisplayFormatter fmt)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(signers);
        ArgumentNullException.ThrowIfNull(fmt);

        container.Column(column =>
        {
            column.Spacing(10);

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(110);
                    c.RelativeColumn();
                    c.ConstantColumn(110);
                    c.RelativeColumn();
                });

                foreach ((string label, string value) in fields)
                {
                    table.Cell().PaddingVertical(1).Text(label).SemiBold();
                    table.Cell().PaddingVertical(1).Text(value);
                }
            });

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(22);
                    c.RelativeColumn(4);
                    c.RelativeColumn(1.2f);
                    c.RelativeColumn(0.8f);
                    c.RelativeColumn(1.4f);
                    c.RelativeColumn(1.4f);
                    c.RelativeColumn(1.6f);
                });
                table.Header(h =>
                {
                    foreach (string title in LineHeaders)
                    {
                        h.Cell().Element(PdfLayout.HeaderCell).Text(title).Bold();
                    }
                });

                foreach (InventoryDocumentLineResponse line in document.Lines ?? [])
                {
                    table.Cell().Element(PdfLayout.Cell).Text(line.LineNumber.ToString(CultureInfo.InvariantCulture));
                    table.Cell().Element(PdfLayout.Cell).Text($"{line.ItemCode} — {line.ItemName}");
                    table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(line.Quantity, 3));
                    table.Cell().Element(PdfLayout.Cell).Text(line.UomCode);
                    table.Cell().Element(PdfLayout.Cell).AlignRight().Text($"{fmt.Number(line.BaseQuantity, 3)} {line.BaseUomCode}");
                    table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(line.UnitCost));
                    table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(line.Value));
                }

                table.Cell().ColumnSpan(6).Element(PdfLayout.Cell).AlignRight().Text("Total").Bold();
                table.Cell().Element(PdfLayout.Cell).AlignRight().Text(fmt.Number(document.TotalValue)).Bold();
            });

            if (!string.IsNullOrWhiteSpace(document.Notes))
            {
                column.Item().Text("Notes").Bold().FontSize(10);
                column.Item().Text(document.Notes);
            }

            column.Item().PaddingTop(25).Row(row =>
            {
                foreach (string signer in signers)
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
