using Application.Abstractions.Paging;
using ClosedXML.Excel;
using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;
using SharedKernel;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;

namespace Web.App.IntegrationTests;

/// <summary>
/// PhaseW2: export foundation (PLAN-WEBAPP §5): Excel/PDF list exporters and the paged collector.
/// </summary>
public sealed class ExportTests
{
    private static readonly ExportHeader Header = new(
        "Items", "items", ["Branch: All branches"], "PT Test", "Bandung", "BDG", "Admin", new DateTime(2026, 10, 3, 14, 5, 0, DateTimeKind.Unspecified));

    private static readonly ExportColumn<Row>[] Columns =
    [
        new("Code", r => r.Code),
        new("Price", r => r.Price, ExportFormat.Money),
        new("Since", r => r.Since, ExportFormat.Date),
        new("Active", r => r.Active, ExportFormat.Boolean)
    ];

    private static readonly Row[] Rows =
    [
        new("ITM-001", 1234.5m, new DateOnly(2026, 1, 2), true),
        new("ITM-002", 99m, new DateOnly(2026, 2, 3), false)
    ];

    private readonly DisplayFormatter _formatter = new(Options.Create(new AppOptions()));

    static ExportTests() => QuestPDF.Settings.License = LicenseType.Community;

    [Fact]
    public void Excel_Should_WriteTitle_Filters_Headers_AndTypedValues()
    {
        byte[] bytes = ExcelExporter.Export(Header, Columns, Rows);

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        IXLWorksheet sheet = workbook.Worksheets.First();
        string text = string.Join('|', sheet.CellsUsed().Select(c => c.GetString()));

        text.ShouldContain("Items");
        text.ShouldContain("Branch: All branches");
        text.ShouldContain("ITM-002");
        IXLCell priceCell = sheet.CellsUsed().Single(c => c.GetString() == "Price");
        IXLCell firstPrice = sheet.Cell(priceCell.Address.RowNumber + 1, priceCell.Address.ColumnNumber);
        firstPrice.DataType.ShouldBe(XLDataType.Number);
        firstPrice.GetValue<decimal>().ShouldBe(1234.5m);
    }

    [Fact]
    public void Pdf_Should_ProduceADocument()
    {
        byte[] bytes = new PdfListExporter(_formatter).Export(Header, Columns, Rows);

        bytes.Length.ShouldBeGreaterThan(1000);
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
    }

    [Fact]
    public void Pdf_Should_FormatValues_TheIndonesianWay()
    {
        var exporter = new PdfListExporter(_formatter);

        exporter.Format(1234.5m, ExportFormat.Money).ShouldBe("1.234,50");
        exporter.Format(new DateOnly(2026, 1, 2), ExportFormat.Date).ShouldBe("02/01/2026");
        exporter.Format(true, ExportFormat.Boolean).ShouldBe("Yes");
        exporter.Format(null, ExportFormat.Text).ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Collector_Should_ReadEveryPage()
    {
        int[] all = [.. Enumerable.Range(1, 250)];
        var requested = new List<int>();

        Result<IReadOnlyList<int>> result = await PagedExportRunner.CollectAsync(
            (paging, _) =>
            {
                requested.Add(paging.Page);
                return Task.FromResult(Result.Success(Page(all, paging)));
            },
            null,
            1000,
            CancellationToken.None);

        result.Value.ShouldBe(all);
        requested.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Collector_Should_Refuse_MoreRowsThanTheCap()
    {
        int[] all = [.. Enumerable.Range(1, 150)];

        Result<IReadOnlyList<int>> result = await PagedExportRunner.CollectAsync(
            (paging, _) => Task.FromResult(Result.Success(Page(all, paging))), null, 100, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Export.TooManyRows");
    }

    [Theory]
    [InlineData("xlsx", ExportFileFormat.Excel)]
    [InlineData("pdf", ExportFileFormat.Pdf)]
    public void FileFormat_Should_Parse(string value, ExportFileFormat expected)
    {
        ExportFileFormats.TryParse(value, out ExportFileFormat format).ShouldBeTrue();
        format.ShouldBe(expected);
    }

    [Fact]
    public void FileFormat_Should_RejectUnknown() => ExportFileFormats.TryParse("csv", out _).ShouldBeFalse();

    private static PagedList<int> Page(int[] all, PageRequest paging) =>
        new([.. all.Skip(paging.Offset).Take(paging.PageSize)], paging.Page, paging.PageSize, all.Length);

    private sealed record Row(string Code, decimal Price, DateOnly Since, bool Active);
}
