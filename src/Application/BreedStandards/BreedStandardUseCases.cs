using System.Data.Common;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using Domain.MasterData.BreedStandards;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.BreedStandards;

/// <summary>
/// Master Breed Standard (PLAN-MOBILE M-69 s.d. M-73), maintained in the WebApp; the mobile app only reads it through
/// the cycle performance. The first standard becomes the default when there is none yet.
/// </summary>
public sealed record CreateBreedStandardCommand(
    string Code,
    string Name,
    string? Strain,
    string? Notes,
    IReadOnlyList<StandardPoint> Rows) : ICommand<Guid>;

/// <summary>
/// Updates the header and replaces all rows; a running cycle sees the change at once, a closed one keeps its frozen copy.
/// </summary>
public sealed record UpdateBreedStandardCommand(
    Guid BreedStandardId,
    string Name,
    string? Strain,
    string? Notes,
    bool IsActive,
    IReadOnlyList<StandardPoint> Rows) : ICommand;

/// <summary>
/// Replaces all rows from a CSV (<see cref="BreedStandardCsv"/>); returns the number of rows.
/// </summary>
public sealed record ImportBreedStandardRowsCommand(Guid BreedStandardId, string Csv) : ICommand<int>;

/// <summary>
/// Makes the standard the default for cycles without a standard of their own.
/// </summary>
public sealed record SetDefaultBreedStandardCommand(Guid BreedStandardId) : ICommand;

public sealed record GetBreedStandardsQuery(bool ActiveOnly = false) : IQuery<IReadOnlyList<BreedStandardResponse>>;

public sealed record GetBreedStandardByIdQuery(Guid BreedStandardId) : IQuery<BreedStandardDetailResponse>;

/// <param name="CycleCount">Cycles that chose the standard (not counting those on the default).</param>
public sealed record BreedStandardResponse(
    Guid Id,
    string Code,
    string Name,
    string? Strain,
    bool IsActive,
    bool IsDefault,
    int RowCount,
    int? MaxAgeDays,
    int CycleCount);

public sealed record BreedStandardDetailResponse(
    Guid Id,
    string Code,
    string Name,
    string? Strain,
    string? Notes,
    bool IsActive,
    bool IsDefault,
    IReadOnlyList<StandardPoint> Rows);

internal static class BreedStandardRules
{
    public static async Task<Result<BreedStandard>> LoadAsync(
        IApplicationDbContext context,
        Guid breedStandardId,
        CancellationToken cancellationToken)
    {
        BreedStandard? standard = await context.BreedStandards
            .Include(s => s.Rows)
            .SingleOrDefaultAsync(s => s.Id == breedStandardId, cancellationToken);

        return standard is null ? Result.Failure<BreedStandard>(BreedStandardErrors.NotFound(breedStandardId)) : standard;
    }
}

internal sealed class CreateBreedStandardCommandValidator : AbstractValidator<CreateBreedStandardCommand>
{
    public CreateBreedStandardCommandValidator()
    {
        RuleFor(c => c.Code).NotEmpty().MaximumLength(20);
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Strain).MaximumLength(100);
        RuleFor(c => c.Notes).MaximumLength(1000);
        RuleFor(c => c.Rows).NotNull();
    }
}

internal sealed class UpdateBreedStandardCommandValidator : AbstractValidator<UpdateBreedStandardCommand>
{
    public UpdateBreedStandardCommandValidator()
    {
        RuleFor(c => c.BreedStandardId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Strain).MaximumLength(100);
        RuleFor(c => c.Notes).MaximumLength(1000);
        RuleFor(c => c.Rows).NotNull();
    }
}

internal sealed class ImportBreedStandardRowsCommandValidator : AbstractValidator<ImportBreedStandardRowsCommand>
{
    public ImportBreedStandardRowsCommandValidator()
    {
        RuleFor(c => c.BreedStandardId).NotEmpty();
        RuleFor(c => c.Csv).NotEmpty().MaximumLength(200_000);
    }
}

internal sealed class CreateBreedStandardCommandHandler(IApplicationDbContext context)
    : ICommandHandler<CreateBreedStandardCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateBreedStandardCommand command, CancellationToken cancellationToken)
    {
        Result<BreedStandard> standard = BreedStandard.Create(command.Code, command.Name, command.Strain, command.Notes, command.Rows);
        if (standard.IsFailure)
        {
            return Result.Failure<Guid>(standard.Error);
        }

        if (await context.BreedStandards.AnyAsync(s => s.Code == standard.Value.Code, cancellationToken))
        {
            return Result.Failure<Guid>(BreedStandardErrors.CodeNotUnique(standard.Value.Code));
        }

        if (!await context.BreedStandards.AnyAsync(s => s.IsDefault, cancellationToken))
        {
            standard.Value.MakeDefault();
        }

        context.BreedStandards.Add(standard.Value);

        await context.SaveChangesAsync(cancellationToken);

        return standard.Value.Id;
    }
}

internal sealed class UpdateBreedStandardCommandHandler(IApplicationDbContext context)
    : ICommandHandler<UpdateBreedStandardCommand>
{
    public async Task<Result> Handle(UpdateBreedStandardCommand command, CancellationToken cancellationToken)
    {
        Result<BreedStandard> standard = await BreedStandardRules.LoadAsync(context, command.BreedStandardId, cancellationToken);
        if (standard.IsFailure)
        {
            return standard;
        }

        Result result = standard.Value.Update(command.Name, command.Strain, command.Notes, command.IsActive);
        if (result.IsSuccess)
        {
            result = standard.Value.ReplaceRows(command.Rows);
        }

        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class ImportBreedStandardRowsCommandHandler(IApplicationDbContext context)
    : ICommandHandler<ImportBreedStandardRowsCommand, int>
{
    public async Task<Result<int>> Handle(ImportBreedStandardRowsCommand command, CancellationToken cancellationToken)
    {
        Result<List<StandardPoint>> rows = BreedStandardCsv.Parse(command.Csv);
        if (rows.IsFailure)
        {
            return Result.Failure<int>(rows.Error);
        }

        Result<BreedStandard> standard = await BreedStandardRules.LoadAsync(context, command.BreedStandardId, cancellationToken);
        if (standard.IsFailure)
        {
            return Result.Failure<int>(standard.Error);
        }

        Result replaced = standard.Value.ReplaceRows(rows.Value);
        if (replaced.IsFailure)
        {
            return Result.Failure<int>(replaced.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        return rows.Value.Count;
    }
}

internal sealed class SetDefaultBreedStandardCommandHandler(IApplicationDbContext context)
    : ICommandHandler<SetDefaultBreedStandardCommand>
{
    public async Task<Result> Handle(SetDefaultBreedStandardCommand command, CancellationToken cancellationToken)
    {
        BreedStandard? standard = await context.BreedStandards
            .SingleOrDefaultAsync(s => s.Id == command.BreedStandardId, cancellationToken);

        if (standard is null)
        {
            return Result.Failure(BreedStandardErrors.NotFound(command.BreedStandardId));
        }

        Result result = standard.MakeDefault();
        if (result.IsFailure)
        {
            return result;
        }

        List<BreedStandard> others = await context.BreedStandards
            .Where(s => s.IsDefault && s.Id != standard.Id)
            .ToListAsync(cancellationToken);

        others.ForEach(s => s.ClearDefault());

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class GetBreedStandardsQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetBreedStandardsQuery, IReadOnlyList<BreedStandardResponse>>
{
    public async Task<Result<IReadOnlyList<BreedStandardResponse>>> Handle(GetBreedStandardsQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        IEnumerable<BreedStandardResponse> standards = await connection.QueryAsync<BreedStandardResponse>(new CommandDefinition(
            """
            SELECT s.id AS Id, s.code AS Code, s.name AS Name, s.strain AS Strain, s.is_active AS IsActive,
                   s.is_default AS IsDefault,
                   (SELECT COUNT(*)::int FROM master.breed_standard_rows r WHERE r.breed_standard_id = s.id) AS RowCount,
                   (SELECT MAX(r.age_days) FROM master.breed_standard_rows r WHERE r.breed_standard_id = s.id) AS MaxAgeDays,
                   (SELECT COUNT(*)::int FROM partnership.production_cycles c WHERE c.breed_standard_id = s.id) AS CycleCount
            FROM master.breed_standards s
            WHERE NOT @ActiveOnly OR s.is_active
            ORDER BY s.is_default DESC, s.code
            """,
            new { query.ActiveOnly },
            cancellationToken: cancellationToken));

        return standards.ToList();
    }
}

internal sealed class GetBreedStandardByIdQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetBreedStandardByIdQuery, BreedStandardDetailResponse>
{
    public async Task<Result<BreedStandardDetailResponse>> Handle(GetBreedStandardByIdQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        Header? header = await connection.QuerySingleOrDefaultAsync<Header>(new CommandDefinition(
            """
            SELECT s.id AS Id, s.code AS Code, s.name AS Name, s.strain AS Strain, s.notes AS Notes,
                   s.is_active AS IsActive, s.is_default AS IsDefault
            FROM master.breed_standards s
            WHERE s.id = @BreedStandardId
            """,
            new { query.BreedStandardId },
            cancellationToken: cancellationToken));

        if (header is null)
        {
            return Result.Failure<BreedStandardDetailResponse>(BreedStandardErrors.NotFound(query.BreedStandardId));
        }

        BreedStandardSet set = await BreedStandardReader.LoadAsync(connection, [header.Id], cancellationToken);
        BreedStandardSnapshot snapshot = set.Resolve(header.Id, null)!.Snapshot;

        return new BreedStandardDetailResponse(
            header.Id, header.Code, header.Name, header.Strain, header.Notes, header.IsActive, header.IsDefault, snapshot.Rows);
    }

    internal sealed class Header
    {
        public Guid Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string? Strain { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
    }
}
