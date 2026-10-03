using Application.Inventory;
using SharedKernel;
using Web.App.Areas.MasterData.Models;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Areas.Inventory;

/// <summary>
/// List page, filters and export columns shared by goods receipts, stock transfers and stock returns.
/// </summary>
public static class InventoryLists
{
    public static ListViewModel<InventoryDocumentResponse> ListModel(
        PagedList<InventoryDocumentResponse> rows, string? search, BranchFilter branchFilter, DateOnly? from, DateOnly? to)
    {
        ArgumentNullException.ThrowIfNull(branchFilter);

        return new ListViewModel<InventoryDocumentResponse>
        {
            Rows = rows,
            Search = search,
            BranchOptions = branchFilter.Options,
            Filters = new Dictionary<string, string?>
            {
                ["from"] = from?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                ["to"] = to?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            }
        };
    }

    public static List<string> Filters(BranchFilter branchFilter, DateOnly? from, DateOnly? to, DisplayFormatter formatter)
    {
        ArgumentNullException.ThrowIfNull(branchFilter);
        ArgumentNullException.ThrowIfNull(formatter);

        List<string> filters = [branchFilter.Description];
        if (from is not null || to is not null)
        {
            filters.Add($"Date: {(from is { } f ? formatter.Date(f) : "…")} – {(to is { } t ? formatter.Date(t) : "…")}");
        }

        return filters;
    }

    /// <param name="referenceTitle">Header of the reference column (PO number or source warehouse).</param>
    /// <param name="partyTitle">Header of the party column (vendor), or null when the documents have none.</param>
    public static ExportColumn<InventoryDocumentResponse>[] Columns(string referenceTitle, string? partyTitle) =>
    [
        new("Number", d => d.Number, Width: 1.4f),
        new("Date", d => d.Date, ExportFormat.Date, 1),
        new("Branch", d => d.BranchCode, Width: 0.8f),
        new(referenceTitle, d => d.Reference, Width: 1.4f),
        .. partyTitle is null ? Array.Empty<ExportColumn<InventoryDocumentResponse>>() : [new(partyTitle, d => d.Party, Width: 2.5f)],
        new("Warehouse", d => d.WarehouseCode, Width: 1.2f),
        new("Cycle", d => d.CycleNumber, Width: 1.3f),
        new("Value", d => d.TotalValue, ExportFormat.Money, 1.5f)
    ];
}
