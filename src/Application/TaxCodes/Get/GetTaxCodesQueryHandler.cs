using System.Data.Common;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using SharedKernel;

namespace Application.TaxCodes.Get;

internal sealed class GetTaxCodesQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetTaxCodesQuery, IReadOnlyList<TaxCodeResponse>>
{
    public async Task<Result<IReadOnlyList<TaxCodeResponse>>> Handle(GetTaxCodesQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            """
            SELECT t.id AS Id, t.code AS Code, t.name AS Name, t.type AS Type, t.vat_treatment AS VatTreatment,
                   t.income_tax_article AS IncomeTaxArticle, t.is_active AS IsActive
            FROM master.tax_codes t
            ORDER BY t.code;

            SELECT r.tax_code_id AS TaxCodeId, r.effective_from AS EffectiveFrom, r.rate_percent AS RatePercent,
                   r.tax_base_ratio AS TaxBaseRatio
            FROM master.tax_rates r
            ORDER BY r.effective_from;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        List<TaxCodeResponse> taxCodes = [.. await multi.ReadAsync<TaxCodeResponse>()];

        ILookup<Guid, TaxRateResponse> ratesByCode = (await multi.ReadAsync<TaxRateRow>())
            .ToLookup(r => r.TaxCodeId, r => new TaxRateResponse(r.EffectiveFrom, r.RatePercent, r.TaxBaseRatio));

        return taxCodes.Select(t => t with { Rates = [.. ratesByCode[t.Id]] }).ToList();
    }

    private sealed record TaxRateRow(Guid TaxCodeId, DateOnly EffectiveFrom, decimal RatePercent, decimal TaxBaseRatio);
}
