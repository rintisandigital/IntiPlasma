using System.Globalization;
using SharedKernel;

namespace Domain.Production.LiveBirdStock;

public static class LiveBirdStockErrors
{
    public static Error NotFound(Guid entryId) => Error.NotFound(
        "LiveBirdStock.NotFound",
        $"The live bird stock entry with the Id = '{entryId}' was not found");

    public static readonly Error InvalidQuantity = Error.Problem(
        "LiveBirdStock.InvalidQuantity",
        "Birds and weight must both be greater than zero");

    public static Error AverageOutsideRange(decimal averageWeightKg, string rangeCode) => Error.Problem(
        "LiveBirdStock.AverageOutsideRange",
        string.Create(CultureInfo.InvariantCulture, $"The average weight {averageWeightKg:0.###} kg is outside the weight range '{rangeCode}'"));

    public static Error PopulationExceeded(int totalBirds, int population) => Error.Problem(
        "LiveBirdStock.PopulationExceeded",
        string.Create(CultureInfo.InvariantCulture, $"The birds of all weight ranges ({totalBirds}) exceed the current population of {population}"));

    public static Error FutureDate(DateOnly date) => Error.Problem(
        "LiveBirdStock.FutureDate",
        $"Live bird stock cannot be reported for the future date {date:yyyy-MM-dd}");

    public static readonly Error KeyMismatch = Error.Problem(
        "LiveBirdStock.KeyMismatch",
        "The cycle and weight range of an entry cannot change; delete it and enter a new one");

    public static readonly Error IdBelongsToOtherEntry = Error.Conflict(
        "LiveBirdStock.IdBelongsToOtherEntry",
        "The client-generated id is already used by an entry of another cycle, date or weight range");

    public static readonly Error TooOldToDelete = Error.Problem(
        "LiveBirdStock.TooOldToDelete",
        "Only entries of today or yesterday can be deleted");
}
