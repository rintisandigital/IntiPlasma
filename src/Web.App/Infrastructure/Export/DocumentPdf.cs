using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Infrastructure.Export;

/// <summary>
/// Building blocks of printed documents (PLAN-WEBAPP §5.3): header fields, line table header, totals, amount in
/// words (W-17), notes and signature boxes.
/// </summary>
public static class DocumentPdf
{
    public static void Compose(IContainer container, Action<ColumnDescriptor> content)
    {
        ArgumentNullException.ThrowIfNull(container);
        container.Column(column =>
        {
            column.Spacing(10);
            content(column);
        });
    }

    public static void Fields(IContainer container, IReadOnlyList<(string Label, string Value)> fields) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(95);
                c.RelativeColumn();
                c.ConstantColumn(95);
                c.RelativeColumn();
            });

            foreach ((string label, string value) in fields)
            {
                table.Cell().PaddingVertical(1).Text(label).SemiBold();
                table.Cell().PaddingVertical(1).Text(value);
            }
        });

    public static void Header(TableDescriptor table, IEnumerable<string> titles) =>
        table.Header(h =>
        {
            foreach (string title in titles)
            {
                h.Cell().Element(PdfLayout.HeaderCell).Text(title).Bold();
            }
        });

    public static void Totals(ColumnDescriptor column, IReadOnlyList<(string Label, decimal Value, bool Bold)> totals) =>
        column.Item().AlignRight().Width(260).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn();
                c.RelativeColumn();
            });

            foreach ((string label, decimal value, bool bold) in totals)
            {
                TextBlockDescriptor l = table.Cell().PaddingVertical(2).Text(label);
                TextBlockDescriptor v = table.Cell().PaddingVertical(2).AlignRight().Text($"Rp {value.ToString("N2", CultureInfo.GetCultureInfo("id-ID"))}");
                if (bold)
                {
                    l.Bold();
                    v.Bold();
                }
            }
        });

    public static void InWords(ColumnDescriptor column, decimal amount) =>
        column.Item().Background(PdfLayout.HeaderFill).Padding(6).Text(text =>
        {
            text.Span("Terbilang: ").SemiBold();
            text.Span(Terbilang.Rupiah(amount)).Italic();
        });

    public static void Notes(ColumnDescriptor column, string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return;
        }

        column.Item().Text("Notes").Bold().FontSize(10);
        column.Item().Text(notes);
    }

    public static void Signatures(ColumnDescriptor column, IReadOnlyList<string> signers) =>
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
}
