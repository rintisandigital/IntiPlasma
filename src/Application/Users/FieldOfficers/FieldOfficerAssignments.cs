using System.Data.Common;
using Application.Abstractions.Auditing;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using Domain.Auditing;
using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.FieldOfficers;

/// <summary>
/// Farmers and coops of a branch assigned directly to a PPL, or unassigned when <see cref="FieldOfficerId"/> is
/// null (Web.App Partnership → Field Officer Assignment, PLAN-MOBILE M-39).
/// </summary>
public sealed record GetFieldOfficerAssignmentsQuery(Guid BranchId, Guid? FieldOfficerId)
    : IQuery<FieldOfficerAssignmentsResponse>;

public sealed record FieldOfficerAssignmentsResponse(
    IReadOnlyList<AssignedFarmer> Farmers,
    IReadOnlyList<AssignedCoop> Coops);

public sealed record AssignedFarmer
{
    public Guid Id { get; init; }

    public string Code { get; init; }

    public string Name { get; init; }

    public string Type { get; init; }

    public bool IsActive { get; init; }
}

public sealed record AssignedCoop
{
    public Guid Id { get; init; }

    public string Code { get; init; }

    public string Name { get; init; }

    public string FarmerCode { get; init; }

    public string FarmerName { get; init; }

    public bool IsActive { get; init; }
}

internal sealed class GetFieldOfficerAssignmentsQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetFieldOfficerAssignmentsQuery, FieldOfficerAssignmentsResponse>
{
    public async Task<Result<FieldOfficerAssignmentsResponse>> Handle(
        GetFieldOfficerAssignmentsQuery query,
        CancellationToken cancellationToken)
    {
        Result access = await branchAccess.EnsureAccessAsync(query.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<FieldOfficerAssignmentsResponse>(access.Error);
        }

        const string sql =
            """
            SELECT f.id AS Id, f.code AS Code, f.name AS Name, f.type AS Type, f.is_active AS IsActive
            FROM master.farmers f
            WHERE f.branch_id = @BranchId
              AND f.field_officer_user_id IS NOT DISTINCT FROM @FieldOfficerId
            ORDER BY f.code;

            SELECT c.id AS Id, c.code AS Code, c.name AS Name, f.code AS FarmerCode, f.name AS FarmerName,
                   c.is_active AS IsActive
            FROM master.coops c
            JOIN master.farmers f ON f.id = c.farmer_id
            WHERE c.branch_id = @BranchId
              AND c.field_officer_user_id IS NOT DISTINCT FROM @FieldOfficerId
            ORDER BY c.code;
            """;

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { query.BranchId, query.FieldOfficerId },
            cancellationToken: cancellationToken));

        List<AssignedFarmer> farmers = [.. await multi.ReadAsync<AssignedFarmer>()];
        List<AssignedCoop> coops = [.. await multi.ReadAsync<AssignedCoop>()];

        return new FieldOfficerAssignmentsResponse(farmers, coops);
    }
}

/// <summary>
/// Moves farmers and coops of one branch from one PPL (or unassigned) to another PPL (or unassigned), e.g. when an
/// employee moves (PLAN-MOBILE M-39). Only records still assigned to <see cref="FromUserId"/> are moved. Recorded
/// in the audit trail. Returns the number of moved records.
/// </summary>
public sealed record ReassignFieldOfficerCommand(
    Guid BranchId,
    Guid? FromUserId,
    Guid? ToUserId,
    IReadOnlyList<Guid> FarmerIds,
    IReadOnlyList<Guid> CoopIds) : ICommand<int>;

internal sealed class ReassignFieldOfficerCommandValidator : AbstractValidator<ReassignFieldOfficerCommand>
{
    public ReassignFieldOfficerCommandValidator()
    {
        RuleFor(c => c.BranchId).NotEmpty();
        RuleFor(c => c.ToUserId).NotEqual(c => c.FromUserId).WithMessage("Choose a different field officer.");
        RuleFor(c => c.FarmerIds).NotNull();
        RuleFor(c => c.CoopIds).NotNull();
        RuleFor(c => c.FarmerIds.Count + c.CoopIds.Count)
            .GreaterThan(0)
            .WithName("Selection")
            .WithMessage("Select at least one farmer or farm.");
    }
}

internal sealed class ReassignFieldOfficerCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IAuditTrail auditTrail) : ICommandHandler<ReassignFieldOfficerCommand, int>
{
    public async Task<Result<int>> Handle(ReassignFieldOfficerCommand command, CancellationToken cancellationToken)
    {
        Result access = await branchAccess.EnsureAccessAsync(command.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<int>(access.Error);
        }

        Result fieldOfficer = await FieldOfficerRules.EnsureValidAsync(
            context, command.ToUserId, command.BranchId, cancellationToken);
        if (fieldOfficer.IsFailure)
        {
            return Result.Failure<int>(fieldOfficer.Error);
        }

        List<Farmer> farmers = await context.Farmers
            .Where(f => command.FarmerIds.Contains(f.Id) && f.BranchId == command.BranchId &&
                        f.FieldOfficerUserId == command.FromUserId)
            .ToListAsync(cancellationToken);

        List<Coop> coops = await context.Coops
            .Where(c => command.CoopIds.Contains(c.Id) && c.BranchId == command.BranchId &&
                        c.FieldOfficerUserId == command.FromUserId)
            .ToListAsync(cancellationToken);

        foreach (Farmer farmer in farmers)
        {
            farmer.AssignFieldOfficer(command.ToUserId);
        }

        foreach (Coop coop in coops)
        {
            coop.AssignFieldOfficer(command.ToUserId);
        }

        int moved = farmers.Count + coops.Count;
        if (moved == 0)
        {
            return 0;
        }

        auditTrail.Record(new AuditEntry(
            AuditCategory.Access,
            "ReassignFieldOfficer",
            $"Field officer reassigned for {farmers.Count} farmer(s) and {coops.Count} farm(s)")
        {
            EntityType = "FieldOfficerAssignment",
            EntityId = command.ToUserId ?? command.FromUserId,
            Details = new
            {
                command.BranchId,
                command.FromUserId,
                command.ToUserId,
                Farmers = farmers.Select(f => f.Code).ToArray(),
                Coops = coops.Select(c => c.Code).ToArray()
            }
        });

        await context.SaveChangesAsync(cancellationToken);

        return moved;
    }
}
