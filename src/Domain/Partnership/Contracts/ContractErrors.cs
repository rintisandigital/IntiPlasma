using Domain.Common;
using SharedKernel;

namespace Domain.Partnership.Contracts;

public static class ContractErrors
{
    public static Error NotFound(Guid contractId) => Error.NotFound(
        "Contracts.NotFound",
        $"The contract with the Id = '{contractId}' was not found");

    public static Error CodeNotUnique(string code) => CommonErrors.CodeNotUnique("Contract", code);

    public static Error NotDraft(Guid contractId) => Error.Problem(
        "Contracts.NotDraft",
        $"The contract with the Id = '{contractId}' is not a draft; its terms can no longer be changed");

    public static Error NotActive(Guid contractId) => Error.Problem(
        "Contracts.NotActive",
        $"The contract with the Id = '{contractId}' is not active");

    public static Error NotUsable(Guid contractId, DateOnly date) => Error.Problem(
        "Contracts.NotUsable",
        $"The contract with the Id = '{contractId}' is not active or not valid on {date:yyyy-MM-dd}");

    public static Error IncentiveRangeRequired(string incentiveName) => Error.Problem(
        "Contracts.IncentiveRangeRequired",
        $"The incentive '{incentiveName}' needs a valid range for its metric");

    public static readonly Error InvalidValidityPeriod = Error.Problem(
        "Contracts.InvalidValidityPeriod",
        "The end of the validity period cannot be before its start");

    public static readonly Error InvalidProfitShare = Error.Problem(
        "Contracts.InvalidProfitShare",
        "A profit sharing contract needs a plasma share between 0 and 100 percent");

    public static readonly Error DuplicateInputPrice = Error.Problem(
        "Contracts.DuplicateInputPrice",
        "An item can only have one contract price");

    public static readonly Error InvalidPrice = Error.Problem(
        "Contracts.InvalidPrice",
        "Prices and amounts must be greater than zero");

    public static readonly Error InvalidWeightRange = Error.Problem(
        "Contracts.InvalidWeightRange",
        "Each weight range needs a non-negative minimum below its maximum");

    public static readonly Error OverlappingWeightRanges = Error.Problem(
        "Contracts.OverlappingWeightRanges",
        "Live bird price weight ranges cannot overlap");

    public static readonly Error PriceContractIncomplete = Error.Problem(
        "Contracts.PriceContractIncomplete",
        "A price contract needs sapronak contract prices and live bird prices before it can be activated");

    public static readonly Error BranchMismatch = Error.Problem(
        "Contracts.BranchMismatch",
        "The contract belongs to a different branch than the coop");
}
