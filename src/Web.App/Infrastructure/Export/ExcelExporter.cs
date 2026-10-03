using ClosedXML.Excel;

namespace Web.App.Infrastructure.Export;

/// <summary>
/// List export to .xlsx (ClosedXML): title block, bold frozen header row with autofilter, real numbers and
/// dates with formats (shown in the reader's Excel locale), auto-fitted columns.
/// </summary>
public static class ExcelExporter
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const int MaxColumnWidth = 60;

    public static byte[] Export<T>(ExportHeader header, IReadOnlyList<ExportColumn<T>> columns, IReadOnlyList<T> rows)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);

        using var workbook = new XLWorkbook();
        IXLWorksheet sheet = workbook.Worksheets.Add(SheetName(header.Title));

        int row = 1;
        sheet.Cell(row, 1).Value = header.Title;
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 1).Style.Font.FontSize = 14;
        row++;

        sheet.Cell(row++, 1).Value = header.CompanyName + (header.BranchName is null ? string.Empty : $" — {header.BranchName}");
        foreach (string filter in header.Filters)
        {
            sheet.Cell(row++, 1).Value = filter;
        }

        sheet.Cell(row++, 1).Value = $"Printed by {header.PrintedBy} on {header.PrintedAtLocal:dd/MM/yyyy HH:mm}";
        row++;

        int headerRow = row;
        for (int c = 0; c < columns.Count; c++)
        {
            IXLCell cell = sheet.Cell(headerRow, c + 1);
            cell.Value = columns[c].Header;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E7F2FC");
        }

        for (int r = 0; r < rows.Count; r++)
        {
            for (int c = 0; c < columns.Count; c++)
            {
                Write(sheet.Cell(headerRow + 1 + r, c + 1), columns[c].Value(rows[r]), columns[c].Format);
            }
        }

        if (columns.Count > 0)
        {
            sheet.Range(headerRow, 1, headerRow + Math.Max(rows.Count, 1), columns.Count).SetAutoFilter();
            sheet.SheetView.FreezeRows(headerRow);
            sheet.Columns(1, columns.Count).AdjustToContents(headerRow, headerRow + rows.Count);

            foreach (IXLColumn column in sheet.Columns(1, columns.Count))
            {
                column.Width = Math.Min(column.Width, MaxColumnWidth);
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    internal static void Write(IXLCell cell, object? value, ExportFormat format)
    {
        switch (value)
        {
            case null:
                return;
            case decimal number:
                cell.Value = number;
                cell.Style.NumberFormat.Format = format switch
                {
                    ExportFormat.WholeNumber => "#,##0",
                    ExportFormat.Percent => "0.00\"%\"",
                    _ => "#,##0.00"
                };
                return;
            case int or long or double:
                cell.Value = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
                cell.Style.NumberFormat.Format = format == ExportFormat.WholeNumber ? "#,##0" : "#,##0.00";
                return;
            case DateOnly date:
                cell.Value = date.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = "dd/mm/yyyy";
                return;
            case DateTime dateTime:
                cell.Value = dateTime;
                cell.Style.DateFormat.Format = format == ExportFormat.Date ? "dd/mm/yyyy" : "dd/mm/yyyy hh:mm";
                return;
            case bool flag:
                cell.Value = flag ? "Yes" : "No";
                return;
            default:
                cell.Value = value.ToString();
                return;
        }
    }

    /// <summary>
    /// Worksheet names: max 31 characters, no []:*?/\.
    /// </summary>
    private static string SheetName(string title)
    {
        string name = new([.. title.Where(ch => "[]:*?/\\".IndexOf(ch, StringComparison.Ordinal) < 0)]);
        return name.Length > 31 ? name[..31] : name;
    }
}
