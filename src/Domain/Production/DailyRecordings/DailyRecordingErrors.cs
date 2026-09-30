using SharedKernel;

namespace Domain.Production.DailyRecordings;

public static class DailyRecordingErrors
{
    public static Error NotFound(Guid recordingId) => Error.NotFound(
        "DailyRecordings.NotFound",
        $"The daily recording with the Id = '{recordingId}' was not found");

    public static Error AlreadyRecorded(DateOnly date) => Error.Conflict(
        "DailyRecordings.AlreadyRecorded",
        $"The cycle already has a recording for {date:yyyy-MM-dd}; revise it instead");

    public static Error FutureDate(DateOnly date) => Error.Problem(
        "DailyRecordings.FutureDate",
        $"A recording cannot be made for the future date {date:yyyy-MM-dd}");

    public static readonly Error NegativeCount = Error.Problem(
        "DailyRecordings.NegativeCount",
        "Mortality and culling cannot be negative");

    public static readonly Error InvalidBodyWeight = Error.Problem(
        "DailyRecordings.InvalidBodyWeight",
        "The average body weight must be greater than zero");

    public static readonly Error InvalidUsage = Error.Problem(
        "DailyRecordings.InvalidUsage",
        "Each used item must appear once with a positive quantity");

    public static readonly Error UsageItemNotAllowed = Error.Problem(
        "DailyRecordings.UsageItemNotAllowed",
        "Only feed and OVK can be recorded as daily usage");

    public static readonly Error IdBelongsToOtherRecording = Error.Conflict(
        "DailyRecordings.IdBelongsToOtherRecording",
        "The client-generated id is already used by a recording of another cycle or date");
}
