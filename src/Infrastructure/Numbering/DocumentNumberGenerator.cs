using System.Globalization;
using Application.Abstractions.Numbering;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Numbering;

internal sealed class DocumentNumberGenerator(ApplicationDbContext dbContext) : IDocumentNumberGenerator
{
    private static readonly string[] RomanMonths = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII"];

    public async Task<string> NextAsync(
        string prefix,
        string branchCode,
        DateOnly documentDate,
        CancellationToken cancellationToken = default)
    {
        string key = string.Create(
            CultureInfo.InvariantCulture,
            $"{prefix}/{branchCode}/{documentDate:yyyy}/{RomanMonths[documentDate.Month - 1]}");

        // Atomic upsert on the DbContext connection: it joins the ambient EF transaction when there is one,
        // and the row lock serializes concurrent callers for the same key.
        List<long> values = await dbContext.Database
            .SqlQuery<long>(
                $"""
                INSERT INTO infrastructure.document_sequences (key, last_value)
                VALUES ({key}, 1)
                ON CONFLICT (key) DO UPDATE SET last_value = document_sequences.last_value + 1
                RETURNING last_value AS "Value"
                """)
            .ToListAsync(cancellationToken);

        return string.Create(CultureInfo.InvariantCulture, $"{key}/{values[0]:D4}");
    }
}
