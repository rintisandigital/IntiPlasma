using Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;

namespace Application.Abstractions.Numbering;

public static class DocumentNumberExtensions
{
    /// <summary>
    /// Next document number for a branch, e.g. <c>PO/BDG/2026/X/0001</c>.
    /// </summary>
    public static async Task<string> NextForBranchAsync(
        this IDocumentNumberGenerator generator,
        IApplicationDbContext context,
        string prefix,
        Guid branchId,
        DateOnly documentDate,
        CancellationToken cancellationToken)
    {
        string branchCode = await context.Branches
            .Where(b => b.Id == branchId)
            .Select(b => b.Code)
            .SingleAsync(cancellationToken);

        return await generator.NextAsync(prefix, branchCode, documentDate, cancellationToken);
    }
}
