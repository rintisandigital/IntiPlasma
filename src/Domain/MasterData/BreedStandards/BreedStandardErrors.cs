using Domain.Common;
using SharedKernel;

namespace Domain.MasterData.BreedStandards;

public static class BreedStandardErrors
{
    public static Error NotFound(Guid breedStandardId) => Error.NotFound(
        "BreedStandards.NotFound",
        $"The breed standard with the Id = '{breedStandardId}' was not found");

    public static Error CodeNotUnique(string code) => CommonErrors.CodeNotUnique("BreedStandard", code);

    public static Error Inactive(string code) => Error.Problem(
        "BreedStandards.Inactive",
        $"The breed standard '{code}' is not active");

    public static readonly Error NoRows = Error.Problem(
        "BreedStandards.NoRows",
        "A breed standard needs at least one age row");

    public static Error InvalidAge(int ageDays) => Error.Problem(
        "BreedStandards.InvalidAge",
        $"The age {ageDays} is outside 0-{BreedStandard.MaxAgeDays} days");

    public static Error DuplicateAge(int ageDays) => Error.Problem(
        "BreedStandards.DuplicateAge",
        $"The age {ageDays} appears more than once");

    public static Error InvalidValue(int ageDays) => Error.Problem(
        "BreedStandards.InvalidValue",
        $"The figures of age {ageDays} are out of range (body weight above 0, FCR above 0, depletion 0-100, feed not negative)");

    public static Error Decreasing(int ageDays, string figure) => Error.Problem(
        "BreedStandards.Decreasing",
        $"The {figure} of age {ageDays} is lower than at an earlier age");

    public static readonly Error DefaultMustBeActive = Error.Problem(
        "BreedStandards.DefaultMustBeActive",
        "The default breed standard must stay active; make another standard the default first");
}
