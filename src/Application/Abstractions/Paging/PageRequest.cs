namespace Application.Abstractions.Paging;

/// <summary>
/// Normalized paging and search parameters for list queries.
/// </summary>
public sealed record PageRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public PageRequest(int? page, int? pageSize, string? search = null)
    {
        Page = Math.Max(page ?? 1, 1);
        PageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
    }

    public int Page { get; }

    public int PageSize { get; }

    public string? Search { get; }

    public int Offset => (Page - 1) * PageSize;

    /// <summary>
    /// ILIKE pattern for <see cref="Search"/> ("%term%"), with LIKE wildcards in the term escaped.
    /// </summary>
    public string? SearchPattern => Search is null
        ? null
        : $"%{Search.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal)}%";
}
