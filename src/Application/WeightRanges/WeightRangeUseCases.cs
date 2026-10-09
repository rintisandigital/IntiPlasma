using System.Data.Common;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using Domain.MasterData.WeightRanges;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.WeightRanges;

/// <summary>
/// Master Rentang Bobot (PLAN-MOBILE M-23, M-46), maintained in the WebApp; the mobile app only reads the active ones.
/// </summary>
public sealed record CreateWeightRangeCommand(
    string Code,
    string Name,
    decimal? MinWeightKg,
    decimal? MaxWeightKg,
    int SortOrder) : ICommand<Guid>;

/// <summary>
/// The bounds can only change while no live bird stock entry uses the range.
/// </summary>
public sealed record UpdateWeightRangeCommand(
    Guid WeightRangeId,
    string Name,
    decimal? MinWeightKg,
    decimal? MaxWeightKg,
    int SortOrder,
    bool IsActive) : ICommand;

public sealed record GetWeightRangesQuery(bool ActiveOnly = false) : IQuery<IReadOnlyList<WeightRangeResponse>>;

public sealed record WeightRangeResponse(
    Guid Id,
    string Code,
    string Name,
    decimal? MinWeightKg,
    decimal? MaxWeightKg,
    int SortOrder,
    bool IsActive);

internal sealed class CreateWeightRangeCommandValidator : AbstractValidator<CreateWeightRangeCommand>
{
    public CreateWeightRangeCommandValidator()
    {
        RuleFor(c => c.Code).NotEmpty().MaximumLength(20);
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.MinWeightKg).InclusiveBetween(0, 99_999);
        RuleFor(c => c.MaxWeightKg).InclusiveBetween(0, 99_999);
        RuleFor(c => c.SortOrder).InclusiveBetween(0, 9_999);
    }
}

internal sealed class UpdateWeightRangeCommandValidator : AbstractValidator<UpdateWeightRangeCommand>
{
    public UpdateWeightRangeCommandValidator()
    {
        RuleFor(c => c.WeightRangeId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.MinWeightKg).InclusiveBetween(0, 99_999);
        RuleFor(c => c.MaxWeightKg).InclusiveBetween(0, 99_999);
        RuleFor(c => c.SortOrder).InclusiveBetween(0, 9_999);
    }
}

internal static class WeightRangeRules
{
    /// <summary>
    /// Active ranges never overlap (M-46).
    /// </summary>
    public static async Task<Result> EnsureNoOverlapAsync(
        IApplicationDbContext context,
        WeightRange range,
        CancellationToken cancellationToken)
    {
        if (!range.IsActive)
        {
            return Result.Success();
        }

        List<WeightRange> active = await context.WeightRanges
            .Where(r => r.IsActive && r.Id != range.Id)
            .ToListAsync(cancellationToken);

        WeightRange? overlapping = active.Find(range.Overlaps);

        return overlapping is null ? Result.Success() : Result.Failure(WeightRangeErrors.Overlap(overlapping.Code));
    }
}

internal sealed class CreateWeightRangeCommandHandler(IApplicationDbContext context)
    : ICommandHandler<CreateWeightRangeCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateWeightRangeCommand command, CancellationToken cancellationToken)
    {
        Result<WeightRange> range = WeightRange.Create(
            command.Code, command.Name, command.MinWeightKg, command.MaxWeightKg, command.SortOrder);
        if (range.IsFailure)
        {
            return Result.Failure<Guid>(range.Error);
        }

        if (await context.WeightRanges.AnyAsync(r => r.Code == range.Value.Code, cancellationToken))
        {
            return Result.Failure<Guid>(WeightRangeErrors.CodeNotUnique(range.Value.Code));
        }

        Result overlap = await WeightRangeRules.EnsureNoOverlapAsync(context, range.Value, cancellationToken);
        if (overlap.IsFailure)
        {
            return Result.Failure<Guid>(overlap.Error);
        }

        context.WeightRanges.Add(range.Value);

        await context.SaveChangesAsync(cancellationToken);

        return range.Value.Id;
    }
}

internal sealed class UpdateWeightRangeCommandHandler(IApplicationDbContext context)
    : ICommandHandler<UpdateWeightRangeCommand>
{
    public async Task<Result> Handle(UpdateWeightRangeCommand command, CancellationToken cancellationToken)
    {
        WeightRange? range = await context.WeightRanges
            .SingleOrDefaultAsync(r => r.Id == command.WeightRangeId, cancellationToken);

        if (range is null)
        {
            return Result.Failure(WeightRangeErrors.NotFound(command.WeightRangeId));
        }

        if (!range.HasBounds(command.MinWeightKg, command.MaxWeightKg))
        {
            if (await context.LiveBirdStockEntries.AnyAsync(e => e.WeightRangeId == range.Id, cancellationToken))
            {
                return Result.Failure(WeightRangeErrors.InUse);
            }

            Result bounds = range.SetBounds(command.MinWeightKg, command.MaxWeightKg);
            if (bounds.IsFailure)
            {
                return bounds;
            }
        }

        range.Update(command.Name, command.SortOrder, command.IsActive);

        Result overlap = await WeightRangeRules.EnsureNoOverlapAsync(context, range, cancellationToken);
        if (overlap.IsFailure)
        {
            return overlap;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class GetWeightRangesQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetWeightRangesQuery, IReadOnlyList<WeightRangeResponse>>
{
    public async Task<Result<IReadOnlyList<WeightRangeResponse>>> Handle(GetWeightRangesQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        IEnumerable<WeightRangeResponse> ranges = await connection.QueryAsync<WeightRangeResponse>(new CommandDefinition(
            """
            SELECT r.id AS Id, r.code AS Code, r.name AS Name, r.min_weight_kg AS MinWeightKg, r.max_weight_kg AS MaxWeightKg,
                   r.sort_order AS SortOrder, r.is_active AS IsActive
            FROM master.weight_ranges r
            WHERE NOT @ActiveOnly OR r.is_active
            ORDER BY r.sort_order, r.code
            """,
            new { query.ActiveOnly },
            cancellationToken: cancellationToken));

        return ranges.ToList();
    }
}
