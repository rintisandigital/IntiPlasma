using System.Globalization;
using Application.Documents;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SharedKernel;
using Web.App.Infrastructure.Export;

namespace Web.App.Areas.Partnership.Documents;

/// <summary>
/// Building blocks of the printed master data sheets (farmer, farm): grey section bars, "label : value" pairs in two
/// columns, small bordered tables and the photo gallery page.
/// </summary>
public static class DataSheetPdf
{
    public const string GalleryTitle = "Photo Gallery";

    private const string SectionFill = "#EEEEEE";
    private const string TableBorder = "#BDBDBD";
    private const float PhotoHeight = 205;
    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    /// <param name="FileName">Shown as the caption below the photo.</param>
    public sealed record Photo(string FileName, Image Image);

    public static void Section(ColumnDescriptor column, string title) =>
        column.Item().PaddingTop(4).Background(SectionFill).PaddingVertical(4).PaddingHorizontal(6)
            .Text($"• {title.ToUpperInvariant()}").Bold();

    public static void SubTitle(ColumnDescriptor column, string title) =>
        column.Item().PaddingTop(2).PaddingHorizontal(6).Text(title).Bold();

    /// <summary>
    /// Label : value pairs in two columns (left to right, then down), as on the paper form.
    /// </summary>
    public static void Fields(ColumnDescriptor column, IReadOnlyList<(string Label, string? Value)> fields) =>
        column.Item().PaddingHorizontal(6).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(90);
                c.ConstantColumn(10);
                c.RelativeColumn();
                c.ConstantColumn(90);
                c.ConstantColumn(10);
                c.RelativeColumn();
            });

            foreach ((string label, string? value) in fields)
            {
                table.Cell().PaddingVertical(1.5f).Text(label).FontColor(Colors.Grey.Darken2);
                table.Cell().PaddingVertical(1.5f).Text(":");
                table.Cell().PaddingVertical(1.5f).PaddingRight(8).Text(OrDash(value));
            }
        });

    /// <summary>
    /// A small bordered table with a "No." column; shows "—" when there are no rows.
    /// </summary>
    public static void Table(ColumnDescriptor column, IReadOnlyList<string> headers, IReadOnlyList<string?[]> rows) =>
        column.Item().PaddingHorizontal(6).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(30);
                foreach (string _ in headers)
                {
                    c.RelativeColumn();
                }
            });

            table.Header(h =>
            {
                h.Cell().Element(HeaderCell).Text("No.").SemiBold();
                foreach (string header in headers)
                {
                    h.Cell().Element(HeaderCell).Text(header).SemiBold();
                }
            });

            if (rows.Count == 0)
            {
                table.Cell().ColumnSpan((uint)headers.Count + 1).Element(BodyCell).AlignCenter().Text("—");
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                table.Cell().Element(BodyCell).Text((i + 1).ToString(CultureInfo.InvariantCulture));
                foreach (string? value in rows[i])
                {
                    table.Cell().Element(BodyCell).Text(OrDash(value));
                }
            }
        });

    public static void Gallery(IContainer container, string title, IReadOnlyList<Photo> photos)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(photos);

        container.Column(column =>
        {
            column.Spacing(6);

            Section(column, title);

            if (photos.Count == 0)
            {
                column.Item().PaddingVertical(20).AlignCenter().Text("Belum ada foto.").Italic().FontColor(Colors.Grey.Darken1);
                return;
            }

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn();
                    c.RelativeColumn();
                });

                foreach (Photo photo in photos)
                {
                    table.Cell().Padding(5).ShowEntire().Column(cell =>
                    {
                        cell.Item().Height(PhotoHeight).Background(Colors.Grey.Lighten4).AlignCenter().AlignMiddle()
                            .Image(photo.Image).FitArea();
                        cell.Item().PaddingTop(3).AlignCenter().Text(photo.FileName).FontSize(7.5f).FontColor(Colors.Grey.Darken2);
                    });
                }
            });
        });
    }

    /// <summary>
    /// The readable photo attachments in upload order; PDFs, missing files and undecodable images are skipped.
    /// </summary>
    public static async Task<IReadOnlyList<Photo>> PhotosAsync(
        IAttachmentService attachments,
        Guid[] documents,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentNullException.ThrowIfNull(documents);

        List<Photo> photos = [];
        if (documents.Length == 0)
        {
            return photos;
        }

        foreach (AttachmentResponse attachment in await attachments.GetManyAsync(documents, cancellationToken))
        {
            if (attachment.Kind != nameof(Domain.Documents.Attachments.AttachmentKind.Photo))
            {
                continue;
            }

            Result<AttachmentContent> content = await attachments.OpenAsync(attachment.Id, cancellationToken);
            if (content.IsFailure)
            {
                continue;
            }

            await using Stream stream = content.Value.Content;
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);

            try
            {
                photos.Add(new Photo(attachment.FileName, Image.FromBinaryData(buffer.ToArray())));
            }
            catch (QuestPDF.Drawing.Exceptions.DocumentComposeException)
            {
                // Not a decodable image (corrupt upload): leave it out of the gallery.
            }
        }

        return photos;
    }

    /// <summary>
    /// "1.104" / "2,5" (Indonesian grouping, no trailing zeros); null stays null.
    /// </summary>
    public static string? Number(decimal? value) => value?.ToString("#,##0.###", Indonesian);

    public static string? Number(int? value) => value?.ToString("#,##0", Indonesian);

    public static string? WithUnit(string? value, string unit) => value is null ? null : $"{value} {unit}";

    private static string OrDash(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

    private static IContainer HeaderCell(IContainer container) =>
        container.Border(0.5f).BorderColor(TableBorder).Background(SectionFill).PaddingVertical(3).PaddingHorizontal(4);

    private static IContainer BodyCell(IContainer container) =>
        container.Border(0.5f).BorderColor(TableBorder).PaddingVertical(3).PaddingHorizontal(4);
}
