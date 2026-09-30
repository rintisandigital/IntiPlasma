using Application.Abstractions.Data;
using Application.Abstractions.Numbering;
using Domain.Finance.Accounts;
using Domain.Finance.CostCenters;
using Domain.Finance.FiscalPeriods;
using Domain.Finance.Journals;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.Journals;

public sealed record JournalLineRequest(Guid AccountId, Guid? CostCenterId, string? Description, decimal Debit, decimal Credit)
{
    public JournalLineInput ToDomain() => new(AccountId, CostCenterId, Description, new Money(Debit), new Money(Credit));
}

internal sealed class JournalLineRequestValidator : AbstractValidator<JournalLineRequest>
{
    public JournalLineRequestValidator()
    {
        RuleFor(l => l.AccountId).NotEmpty();
        RuleFor(l => l.Debit).GreaterThanOrEqualTo(0);
        RuleFor(l => l.Credit).GreaterThanOrEqualTo(0);
        RuleFor(l => l.Description).MaximumLength(250);
    }
}

/// <summary>
/// Shared rules for building and posting journals, used by manual journals and the auto journal engine.
/// </summary>
internal static class JournalSupport
{
    public const string ManualPrefix = "JU";
    public const string AutomaticPrefix = "JO";

    /// <summary>
    /// Every line must use an active postable account and an active cost center (if any).
    /// </summary>
    public static async Task<Result> ValidateReferencesAsync(
        IApplicationDbContext context,
        IReadOnlyList<JournalLineInput> lines,
        CancellationToken cancellationToken)
    {
        var accountIds = lines.Select(l => l.AccountId).Distinct().ToList();

        List<Guid> postable = await context.Accounts
            .Where(a => accountIds.Contains(a.Id) && a.IsPostable && a.IsActive)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        Guid invalidAccount = accountIds.Find(id => !postable.Contains(id));
        if (invalidAccount != Guid.Empty)
        {
            return Result.Failure(AccountErrors.NotPostable(invalidAccount));
        }

        var costCenterIds = lines.Where(l => l.CostCenterId is not null).Select(l => l.CostCenterId!.Value).Distinct().ToList();

        List<Guid> activeCostCenters = await context.CostCenters
            .Where(c => costCenterIds.Contains(c.Id) && c.IsActive)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        Guid invalidCostCenter = costCenterIds.Find(id => !activeCostCenters.Contains(id));

        return invalidCostCenter == Guid.Empty
            ? Result.Success()
            : Result.Failure(CostCenterErrors.NotFound(invalidCostCenter));
    }

    public static async Task<Result<FiscalPeriod>> FindPeriodAsync(
        IApplicationDbContext context,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        FiscalPeriod? period = await context.FiscalPeriods
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.StartDate <= date && date <= p.EndDate, cancellationToken);

        if (period is null)
        {
            return Result.Failure<FiscalPeriod>(FiscalPeriodErrors.NotFoundForDate(date));
        }

        return period.IsOpen ? period : Result.Failure<FiscalPeriod>(FiscalPeriodErrors.Closed(date));
    }

    public static async Task<string> NextNumberAsync(
        IApplicationDbContext context,
        IDocumentNumberGenerator numberGenerator,
        JournalSource source,
        Guid branchId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        string branchCode = await context.Branches
            .Where(b => b.Id == branchId)
            .Select(b => b.Code)
            .SingleAsync(cancellationToken);

        string prefix = source == JournalSource.Manual ? ManualPrefix : AutomaticPrefix;

        return await numberGenerator.NextAsync(prefix, branchCode, date, cancellationToken);
    }
}
