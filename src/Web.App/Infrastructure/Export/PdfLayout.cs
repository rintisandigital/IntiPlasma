using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Web.App.Infrastructure.Export;

/// <summary>
/// Shared page elements of every PDF (lists, reports, printed documents): letterhead and "Page x of y" footer
/// (PLAN-WEBAPP §5.3).
/// </summary>
public static class PdfLayout
{
    public const string PrimaryColor = "#0984E3";
    public const string HeaderFill = "#E7F2FC";
    public const string LineColor = "#E6EAED";
    public const string TextColor = "#092C4C";

    public static void Page(PageDescriptor page, ExportHeader header, bool landscape)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(header);

        page.Size(landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
        page.Margin(28);
        page.DefaultTextStyle(style => style.FontSize(8.5f).FontColor(TextColor));

        page.Header().Element(container => Letterhead(container, header));

        page.Footer().PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text($"Printed by {header.PrintedBy} · {header.PrintedAtLocal:dd/MM/yyyy HH:mm}")
                .FontSize(7).FontColor(Colors.Grey.Darken1);
            row.RelativeItem().AlignRight().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(7).FontColor(Colors.Grey.Darken1));
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        });
    }

    private static void Letterhead(IContainer container, ExportHeader header) =>
        container.PaddingBottom(10).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(header.CompanyName).Bold().FontSize(11).FontColor(PrimaryColor);
                    if (header.BranchName is not null)
                    {
                        left.Item().Text($"Branch: {header.BranchName}").FontSize(8);
                    }
                });
                row.RelativeItem().AlignRight().Text(header.Title).Bold().FontSize(13);
            });

            foreach (string filter in header.Filters)
            {
                column.Item().AlignRight().Text(filter).FontSize(7.5f).FontColor(Colors.Grey.Darken1);
            }

            column.Item().PaddingTop(6).LineHorizontal(1).LineColor(PrimaryColor);
        });

    /// <summary>
    /// A body cell with a light bottom border.
    /// </summary>
    public static IContainer Cell(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor(LineColor).PaddingVertical(3).PaddingHorizontal(4);

    public static IContainer HeaderCell(IContainer container) =>
        container.Background(HeaderFill).PaddingVertical(4).PaddingHorizontal(4);
}
