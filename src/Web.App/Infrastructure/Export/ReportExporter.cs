using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Web.App.Infrastructure.Export;

/// <summary>
/// Renders a <see cref="ReportDocument"/> to Excel (one worksheet per table, title block, typed rows: bold sections,
/// subtotals with a top border, totals with a double border; real numbers) and to PDF (the same tables in sequence).
/// </summary>
public sealed class ReportExporter(PdfListExporter cells)
{
    private const int MaxColumnWidth = 60;

    public static byte[] Excel(ExportHeader header, ReportDocument report)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(report);

        using var workbook = new XLWorkbook();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (ReportTable table in report.Tables)
        {
            IXLWorksheet sheet = workbook.Worksheets.Add(UniqueSheetName(table.Title ?? header.Title, names));

            int row = 1;
            sheet.Cell(row, 1).Value = header.Title;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 1).Style.Font.FontSize = 14;
            row++;
            if (table.Title is not null && table.Title != header.Title)
            {
                sheet.Cell(row, 1).Value = table.Title;
                sheet.Cell(row++, 1).Style.Font.Bold = true;
            }

            sheet.Cell(row++, 1).Value = header.CompanyName + (header.BranchName is null ? string.Empty : $" — {header.BranchName}");
            foreach (string filter in header.Filters)
            {
                sheet.Cell(row++, 1).Value = filter;
            }

            sheet.Cell(row++, 1).Value = $"Printed by {header.PrintedBy} on {header.PrintedAtLocal:dd/MM/yyyy HH:mm}";
            row++;

            int headerRow = row;
            for (int c = 0; c < table.Columns.Count; c++)
            {
                IXLCell cell = sheet.Cell(headerRow, c + 1);
                cell.Value = table.Columns[c].Header;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml(PdfLayout.HeaderFill);
                if (IsNumeric(table.Columns[c].Format))
                {
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                }
            }

            row = headerRow + 1;
            foreach (ReportRow reportRow in table.Rows)
            {
                for (int c = 0; c < table.Columns.Count; c++)
                {
                    IXLCell cell = sheet.Cell(row, c + 1);
                    object? value = c < reportRow.Cells.Count ? reportRow.Cells[c] : null;
                    ExcelExporter.Write(cell, value, table.Columns[c].Format);
                    Style(cell, reportRow.Kind);
                }

                if (reportRow.Indent > 0)
                {
                    sheet.Cell(row, 1).Style.Alignment.Indent = reportRow.Indent;
                }

                row++;
            }

            foreach (string note in table.Notes ?? [])
            {
                sheet.Cell(++row, 1).Value = note;
                sheet.Cell(row, 1).Style.Font.Italic = true;
            }

            if (table.Columns.Count > 0)
            {
                sheet.SheetView.FreezeRows(headerRow);
                sheet.Columns(1, table.Columns.Count).AdjustToContents(headerRow, Math.Max(headerRow, row));
                foreach (IXLColumn column in sheet.Columns(1, table.Columns.Count))
                {
                    column.Width = Math.Min(column.Width, MaxColumnWidth);
                }
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] Pdf(ExportHeader header, ReportDocument report)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(report);

        return Document.Create(document => document.Page(page =>
        {
            PdfLayout.Page(page, header, report.Landscape);

            page.Content().Column(column =>
            {
                column.Spacing(12);
                foreach (ReportTable table in report.Tables)
                {
                    column.Item().Element(container => Table(container, table, report.Tables.Count > 1));
                }
            });
        })).GeneratePdf();
    }

    private void Table(IContainer container, ReportTable table, bool showTitle) =>
        container.Column(column =>
        {
            if (showTitle && table.Title is not null)
            {
                column.Item().PaddingBottom(4).Text(table.Title).Bold().FontSize(10);
            }

            column.Item().Table(grid =>
            {
                grid.ColumnsDefinition(definition =>
                {
                    foreach (ReportColumn reportColumn in table.Columns)
                    {
                        definition.RelativeColumn(reportColumn.Width);
                    }
                });

                grid.Header(head =>
                {
                    foreach (ReportColumn reportColumn in table.Columns)
                    {
                        IContainer cell = head.Cell().Element(PdfLayout.HeaderCell);
                        (IsNumeric(reportColumn.Format) ? cell.AlignRight() : cell).Text(reportColumn.Header).Bold();
                    }
                });

                foreach (ReportRow row in table.Rows)
                {
                    if (row.Kind == ReportRowKind.Section)
                    {
                        grid.Cell().ColumnSpan((uint)table.Columns.Count).Element(PdfLayout.Cell)
                            .Text(row.Cells.Count > 0 ? row.Cells[0]?.ToString() : string.Empty).SemiBold();
                        continue;
                    }

                    for (int c = 0; c < table.Columns.Count; c++)
                    {
                        object? value = c < row.Cells.Count ? row.Cells[c] : null;
                        IContainer cell = grid.Cell().Element(row.Kind == ReportRowKind.Detail ? PdfLayout.Cell : TotalCell);
                        if (c == 0 && row.Indent > 0)
                        {
                            cell = cell.PaddingLeft(10 * row.Indent);
                        }

                        TextBlockDescriptor text = (IsNumeric(table.Columns[c].Format) ? cell.AlignRight() : cell)
                            .Text(cells.Format(value, table.Columns[c].Format));
                        if (row.Kind != ReportRowKind.Detail)
                        {
                            text.Bold();
                        }
                    }
                }

                if (table.Rows.Count == 0)
                {
                    grid.Cell().ColumnSpan((uint)table.Columns.Count).Element(PdfLayout.Cell).Text("No records.").Italic();
                }
            });

            foreach (string note in table.Notes ?? [])
            {
                column.Item().PaddingTop(4).Text(note).Italic();
            }
        });

    private static IContainer TotalCell(IContainer container) =>
        container.BorderTop(0.75f).BorderBottom(0.5f).BorderColor(PdfLayout.TextColor).Background("#F5F8FB").PaddingVertical(3).PaddingHorizontal(4);

    private static void Style(IXLCell cell, ReportRowKind kind)
    {
        switch (kind)
        {
            case ReportRowKind.Section:
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F8FB");
                break;
            case ReportRowKind.Subtotal:
                cell.Style.Font.Bold = true;
                cell.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                break;
            case ReportRowKind.Total:
                cell.Style.Font.Bold = true;
                cell.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Double;
                break;
            default:
                break;
        }
    }

    private static bool IsNumeric(ExportFormat format) =>
        format is ExportFormat.WholeNumber or ExportFormat.Number or ExportFormat.Money or ExportFormat.Percent;

    private static string UniqueSheetName(string title, HashSet<string> used)
    {
        string name = new([.. title.Where(ch => "[]:*?/\\".IndexOf(ch, StringComparison.Ordinal) < 0)]);
        name = name.Length > 31 ? name[..31] : name;
        string candidate = name;
        int number = 1;
        while (!used.Add(candidate))
        {
            number++;
            string suffix = $" ({number})";
            candidate = (name.Length + suffix.Length > 31 ? name[..(31 - suffix.Length)] : name) + suffix;
        }

        return candidate;
    }
}
