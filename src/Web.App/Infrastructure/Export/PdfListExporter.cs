using QuestPDF.Fluent;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Infrastructure.Export;

/// <summary>
/// List export to PDF (QuestPDF): landscape A4 table from the same columns as the Excel export.
/// </summary>
public sealed class PdfListExporter(DisplayFormatter formatter)
{
    public const string ContentType = "application/pdf";

    public byte[] Export<T>(ExportHeader header, IReadOnlyList<ExportColumn<T>> columns, IReadOnlyList<T> rows)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);

        return Document.Create(document => document.Page(page =>
        {
            PdfLayout.Page(page, header, landscape: true);

            page.Content().Table(table =>
            {
                table.ColumnsDefinition(definition =>
                {
                    foreach (ExportColumn<T> column in columns)
                    {
                        definition.RelativeColumn(column.Width);
                    }
                });

                table.Header(head =>
                {
                    foreach (ExportColumn<T> column in columns)
                    {
                        head.Cell().Element(PdfLayout.HeaderCell).Element(c => Align(c, column.Format)).Text(column.Header).Bold();
                    }
                });

                foreach (T row in rows)
                {
                    foreach (ExportColumn<T> column in columns)
                    {
                        table.Cell().Element(PdfLayout.Cell).Element(c => Align(c, column.Format))
                            .Text(Format(column.Value(row), column.Format));
                    }
                }

                if (rows.Count == 0)
                {
                    table.Cell().ColumnSpan((uint)columns.Count).Element(PdfLayout.Cell).Text("No records.").Italic();
                }
            });
        })).GeneratePdf();
    }

    public string Format(object? value, ExportFormat format) => value switch
    {
        null => string.Empty,
        decimal number => format switch
        {
            ExportFormat.WholeNumber => formatter.Number(number, 0),
            ExportFormat.Money => formatter.Number(number),
            ExportFormat.Percent => formatter.Number(number) + "%",
            _ => formatter.Number(number)
        },
        int number => formatter.Number(number, 0),
        long number => formatter.Number(number, 0),
        DateOnly date => formatter.Date(date),
        DateTime dateTime => formatter.DateTime(dateTime),
        bool flag => flag ? "Yes" : "No",
        _ => value.ToString() ?? string.Empty
    };

    private static QuestPDF.Infrastructure.IContainer Align(QuestPDF.Infrastructure.IContainer container, ExportFormat format) =>
        format is ExportFormat.WholeNumber or ExportFormat.Number or ExportFormat.Money or ExportFormat.Percent
            ? container.AlignRight()
            : container;
}
