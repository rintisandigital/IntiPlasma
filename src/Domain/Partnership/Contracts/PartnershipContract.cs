using Domain.Common;
using SharedKernel;

namespace Domain.Partnership.Contracts;

/// <summary>
/// Kontrak kemitraan inti-plasma. The scheme is chosen per contract:
/// <list type="bullet">
/// <item><see cref="ContractScheme.PriceContract"/>: sapronak are charged to the plasma at contract prices and the
/// harvest is bought back at a guaranteed price per kg (by weight range).</item>
/// <item><see cref="ContractScheme.ProfitSharing"/>: the cycle profit is shared by percentage.</item>
/// </list>
/// Terms are editable only while the contract is a draft. Once active, a contract is immutable;
/// changed terms need a new contract. Production cycles take a snapshot of the terms when they are planned.
/// </summary>
public sealed class PartnershipContract : AggregateRoot, IHasDocuments
{
    private readonly List<ContractInputPrice> _inputPrices = [];
    private readonly List<ContractLiveBirdPrice> _liveBirdPrices = [];
    private readonly List<ContractIncentive> _incentives = [];

    private PartnershipContract(Guid id, string code, Guid branchId, ContractScheme scheme)
        : base(id)
    {
        Code = code;
        BranchId = branchId;
        Scheme = scheme;
        Status = ContractStatus.Draft;
    }

    private PartnershipContract()
    {
    }

    public string Code { get; private set; }
    public string Name { get; private set; }
    public Guid BranchId { get; private set; }
    public ContractScheme Scheme { get; private set; }
    public ContractStatus Status { get; private set; }
    public DateOnly ValidFrom { get; private set; }
    public DateOnly? ValidTo { get; private set; }

    /// <summary>
    /// Plasma's share of the cycle profit in percent (only for <see cref="ContractScheme.ProfitSharing"/>).
    /// </summary>
    public decimal? PlasmaProfitSharePercent { get; private set; }

    /// <summary>
    /// PPh withheld from the plasma settlement payment, if any.
    /// </summary>
    public Guid? IncomeTaxCodeId { get; private set; }

    public string? Notes { get; private set; }

    public IReadOnlyCollection<ContractInputPrice> InputPrices => [.. _inputPrices];
    public IReadOnlyCollection<ContractLiveBirdPrice> LiveBirdPrices => [.. _liveBirdPrices];
    public IReadOnlyCollection<ContractIncentive> Incentives => [.. _incentives];

    /// <summary>
    /// Lampiran: ids of the attached photos and documents.
    /// </summary>
    public Guid[] Documents { get; private set; } = [];

    public Result SetDocuments(IEnumerable<Guid>? documents) =>
        DocumentList.Apply(documents, value => Documents = value);

    public static Result<PartnershipContract> Create(string code, Guid branchId, ContractScheme scheme, ContractTerms terms)
    {
        var contract = new PartnershipContract(Guid.CreateVersion7(), Codes.Normalize(code), branchId, scheme);

        Result result = contract.ApplyTerms(terms);

        return result.IsSuccess ? contract : Result.Failure<PartnershipContract>(result.Error);
    }

    public Result UpdateTerms(ContractTerms terms)
    {
        if (Status != ContractStatus.Draft)
        {
            return Result.Failure(ContractErrors.NotDraft(Id));
        }

        return ApplyTerms(terms);
    }

    public Result Activate()
    {
        if (Status != ContractStatus.Draft)
        {
            return Result.Failure(ContractErrors.NotDraft(Id));
        }

        if (Scheme == ContractScheme.PriceContract && (_inputPrices.Count == 0 || _liveBirdPrices.Count == 0))
        {
            return Result.Failure(ContractErrors.PriceContractIncomplete);
        }

        Status = ContractStatus.Active;

        Raise(new ContractActivatedDomainEvent(Id));

        return Result.Success();
    }

    public Result Deactivate()
    {
        if (Status != ContractStatus.Active)
        {
            return Result.Failure(ContractErrors.NotActive(Id));
        }

        Status = ContractStatus.Inactive;

        return Result.Success();
    }

    public bool IsUsableOn(DateOnly date) =>
        Status == ContractStatus.Active && ValidFrom <= date && (ValidTo is null || date <= ValidTo);

    public ContractSnapshot CreateSnapshot() =>
        new(
            Id,
            Code,
            Scheme,
            PlasmaProfitSharePercent,
            IncomeTaxCodeId,
            [.. _inputPrices.Select(p => new ContractSnapshot.InputPrice(p.ItemId, p.Price))],
            [.. _liveBirdPrices.Select(p => new ContractSnapshot.LiveBirdPrice(p.MinWeightKg, p.MaxWeightKg, p.PricePerKg))],
            [.. _incentives.Select(i => new ContractSnapshot.Incentive(
                i.Name, i.Kind, i.Metric, i.RangeFrom, i.RangeTo, i.Amount, i.Basis))]);

    private Result ApplyTerms(ContractTerms terms)
    {
        Result validation = Validate(terms);
        if (validation.IsFailure)
        {
            return validation;
        }

        Name = terms.Name.Trim();
        ValidFrom = terms.ValidFrom;
        ValidTo = terms.ValidTo;
        PlasmaProfitSharePercent = Scheme == ContractScheme.ProfitSharing ? terms.PlasmaProfitSharePercent : null;
        IncomeTaxCodeId = terms.IncomeTaxCodeId;
        Notes = terms.Notes;

        _inputPrices.Clear();
        _inputPrices.AddRange(terms.InputPrices.Select(p => new ContractInputPrice(Id, p.ItemId, p.Price)));

        _liveBirdPrices.Clear();
        _liveBirdPrices.AddRange(terms.LiveBirdPrices
            .OrderBy(p => p.MinWeightKg)
            .Select(p => new ContractLiveBirdPrice(Id, p.MinWeightKg, p.MaxWeightKg, p.PricePerKg)));

        _incentives.Clear();
        _incentives.AddRange(terms.Incentives.Select((i, index) => new ContractIncentive(
            Id, index + 1, i.Name.Trim(), i.Kind, i.Metric, i.RangeFrom, i.RangeTo, i.Amount, i.Basis)));

        return Result.Success();
    }

    private Result Validate(ContractTerms terms)
    {
        if (terms.ValidTo is not null && terms.ValidTo < terms.ValidFrom)
        {
            return Result.Failure(ContractErrors.InvalidValidityPeriod);
        }

        if (Scheme == ContractScheme.ProfitSharing && terms.PlasmaProfitSharePercent is null or <= 0 or >= 100)
        {
            return Result.Failure(ContractErrors.InvalidProfitShare);
        }

        if (terms.InputPrices.GroupBy(p => p.ItemId).Any(g => g.Count() > 1))
        {
            return Result.Failure(ContractErrors.DuplicateInputPrice);
        }

        if (terms.InputPrices.Any(p => p.Price.IsNegative || p.Price.IsZero))
        {
            return Result.Failure(ContractErrors.InvalidPrice);
        }

        Result liveBirdResult = ValidateLiveBirdPrices(terms.LiveBirdPrices);
        if (liveBirdResult.IsFailure)
        {
            return liveBirdResult;
        }

        return ValidateIncentives(terms.Incentives);
    }

    private static Result ValidateLiveBirdPrices(IReadOnlyList<ContractTerms.LiveBirdPrice> prices)
    {
        if (prices.Any(p => p.MinWeightKg < 0 || p.MaxWeightKg <= p.MinWeightKg))
        {
            return Result.Failure(ContractErrors.InvalidWeightRange);
        }

        if (prices.Any(p => p.PricePerKg.IsNegative || p.PricePerKg.IsZero))
        {
            return Result.Failure(ContractErrors.InvalidPrice);
        }

        var ordered = prices.OrderBy(p => p.MinWeightKg).ToList();
        for (int i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].MinWeightKg < ordered[i - 1].MaxWeightKg)
            {
                return Result.Failure(ContractErrors.OverlappingWeightRanges);
            }
        }

        return Result.Success();
    }

    private static Result ValidateIncentives(IReadOnlyList<ContractTerms.Incentive> incentives)
    {
        foreach (ContractTerms.Incentive incentive in incentives)
        {
            bool needsRange = incentive.Metric != IncentiveMetric.None;

            if (needsRange && incentive.RangeFrom is null && incentive.RangeTo is null)
            {
                return Result.Failure(ContractErrors.IncentiveRangeRequired(incentive.Name));
            }

            if (incentive.RangeFrom is not null && incentive.RangeTo is not null && incentive.RangeTo < incentive.RangeFrom)
            {
                return Result.Failure(ContractErrors.IncentiveRangeRequired(incentive.Name));
            }

            if (incentive.Amount.IsNegative || incentive.Amount.IsZero)
            {
                return Result.Failure(ContractErrors.InvalidPrice);
            }
        }

        return Result.Success();
    }
}
