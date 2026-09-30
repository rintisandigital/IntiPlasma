using SharedKernel;

namespace Application.Finance.Reports;

internal static class ReportErrors
{
    public static readonly Error InvalidDateRange = Error.Problem(
        "Reports.InvalidDateRange",
        "The end date cannot be before the start date");
}
