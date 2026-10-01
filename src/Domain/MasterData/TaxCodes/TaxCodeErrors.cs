using Domain.Common;
using SharedKernel;

namespace Domain.MasterData.TaxCodes;

public static class TaxCodeErrors
{
    public static Error NotFound(Guid taxCodeId) => Error.NotFound(
        "TaxCodes.NotFound",
        $"The tax code with the Id = '{taxCodeId}' was not found");

    public static Error CodeNotUnique(string code) => CommonErrors.CodeNotUnique("TaxCode", code);

    public static Error NotIncomeTax(Guid taxCodeId) => Error.Problem(
        "TaxCodes.NotIncomeTax",
        $"The tax code with the Id = '{taxCodeId}' is not an income tax (PPh) code");

    public static readonly Error RateRequired = Error.Problem(
        "TaxCodes.RateRequired",
        "At least one rate must be provided");

    public static readonly Error DuplicateEffectiveDate = Error.Problem(
        "TaxCodes.DuplicateEffectiveDate",
        "Two rates cannot have the same effective date");

    public static readonly Error InvalidRate = Error.Problem(
        "TaxCodes.InvalidRate",
        "The rate must be between 0 and 100 percent");

    public static Error NoRate(string taxCode, DateOnly date) => Error.Problem(
        "TaxCodes.NoRate",
        $"The tax code {taxCode} has no rate in effect on {date:yyyy-MM-dd}");

    public static readonly Error InvalidTaxBaseRatio = Error.Problem(
        "TaxCodes.InvalidTaxBaseRatio",
        "The tax base ratio (DPP) must be greater than 0 and at most 1");
}
