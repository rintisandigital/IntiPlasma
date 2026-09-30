using SharedKernel;

namespace Domain.Common;

public static class CommonErrors
{
    public static readonly Error InvalidNpwp = Error.Problem(
        "Common.InvalidNpwp",
        "NPWP must consist of 15 or 16 digits");

    public static readonly Error InvalidNitku = Error.Problem(
        "Common.InvalidNitku",
        "NITKU must consist of 22 digits");

    public static readonly Error PkpRequiresNpwp = Error.Problem(
        "Common.PkpRequiresNpwp",
        "A PKP (VAT registered) party must have an NPWP");

    public static readonly Error IncompleteBankAccount = Error.Problem(
        "Common.IncompleteBankAccount",
        "Bank name, account number and account holder name must all be provided");

    public static readonly Error InvalidNik = Error.Problem(
        "Common.InvalidNik",
        "NIK must consist of 16 digits");

    public static Error CodeNotUnique(string entity, string code) => Error.Conflict(
        $"{entity}.CodeNotUnique",
        $"A {entity} with code '{code}' already exists");

    public static Error Inactive(string entity, Guid id) => Error.Problem(
        $"{entity}.Inactive",
        $"The {entity} with the Id = '{id}' is inactive");
}
