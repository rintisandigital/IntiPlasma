using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using Domain.MasterData.Branches;
using Domain.Roles;
using SharedKernel;

namespace Application.Users.FieldOfficers;

/// <summary>
/// PPL that farmers and coops can be assigned to (PLAN-MOBILE §4.5): active users whose role grants
/// <c>partnership:assigned-only</c> (not Administrator) and whose branch access covers the branch. Without a
/// branch: those covering any branch the caller may access.
/// </summary>
public sealed record GetFieldOfficersQuery(Guid? BranchId = null) : IQuery<IReadOnlyList<FieldOfficerResponse>>;

public sealed record FieldOfficerResponse
{
    public Guid Id { get; init; }

    public string Email { get; init; }

    public string FirstName { get; init; }

    public string LastName { get; init; }

    public string Name => $"{FirstName} {LastName}".Trim();
}

internal sealed class GetFieldOfficersQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetFieldOfficersQuery, IReadOnlyList<FieldOfficerResponse>>
{
    public async Task<Result<IReadOnlyList<FieldOfficerResponse>>> Handle(
        GetFieldOfficersQuery query,
        CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        if (query.BranchId is { } branchId && !scope.CanAccess(branchId))
        {
            return Result.Failure<IReadOnlyList<FieldOfficerResponse>>(
                BranchErrors.AccessDenied(branchId));
        }

        string sql =
            $"""
             SELECT u.id AS Id, u.email AS Email, u.first_name AS FirstName, u.last_name AS LastName
             FROM identity.users u
             LEFT JOIN identity.branch_access_profiles bp ON bp.id = u.branch_access_profile_id
             WHERE {FieldOfficerSql.Condition("u")}
               AND bp.id IS NOT NULL
               AND (bp.all_branches
                    OR EXISTS (SELECT 1 FROM identity.branch_access_profile_branches pb
                               WHERE pb.profile_id = bp.id
                                 AND (pb.branch_id = @BranchId
                                      OR (@BranchId::uuid IS NULL AND (@AllBranches OR pb.branch_id = ANY(@BranchIds))))))
             ORDER BY u.first_name, u.last_name, u.email
             """;

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        IEnumerable<FieldOfficerResponse> rows = await connection.QueryAsync<FieldOfficerResponse>(new CommandDefinition(
            sql,
            new
            {
                query.BranchId,
                scope.AllBranches,
                scope.BranchIds,
                FieldOfficerSql.AssignedOnly,
                FieldOfficerSql.AdministratorName
            },
            cancellationToken: cancellationToken));

        return rows.ToList();
    }
}

internal static class FieldOfficerSql
{
    public const string AssignedOnly = Permissions.PartnershipAssignedOnly;

    public const string AdministratorName = Role.AdministratorName;

    /// <summary>
    /// Active user holding the PPL scope permission through a role, outside the system Administrator role. The
    /// query passes <c>@AssignedOnly</c> and <c>@AdministratorName</c>.
    /// </summary>
    public static string Condition(string alias) =>
        $"""
         {alias}.is_active
         AND EXISTS (SELECT 1 FROM identity.user_roles fo_ur
                     JOIN identity.role_permissions fo_rp ON fo_rp.role_id = fo_ur.role_id
                     WHERE fo_ur.user_id = {alias}.id AND fo_rp.permission = @AssignedOnly)
         AND NOT EXISTS (SELECT 1 FROM identity.user_roles fo_ur
                         JOIN identity.roles fo_r ON fo_r.id = fo_ur.role_id
                         WHERE fo_ur.user_id = {alias}.id AND fo_r.is_system AND fo_r.name = @AdministratorName)
         """;
}
