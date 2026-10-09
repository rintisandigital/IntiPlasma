using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;
using Domain.Partnership.Cycles;
using SharedKernel;

namespace Application.Abstractions.Authorization;

public static class FieldScopeExtensions
{
    public static async Task<Result> EnsureFarmerAsync(
        this IFieldScope fieldScope,
        Guid farmerId,
        CancellationToken cancellationToken = default) =>
        await fieldScope.CanAccessFarmerAsync(farmerId, cancellationToken)
            ? Result.Success()
            : Result.Failure(FarmerErrors.NotFound(farmerId));

    public static async Task<Result> EnsureCoopAsync(
        this IFieldScope fieldScope,
        Guid coopId,
        CancellationToken cancellationToken = default) =>
        await fieldScope.CanAccessCoopAsync(coopId, cancellationToken)
            ? Result.Success()
            : Result.Failure(CoopErrors.NotFound(coopId));

    public static async Task<Result> EnsureCycleAsync(
        this IFieldScope fieldScope,
        Guid cycleId,
        CancellationToken cancellationToken = default) =>
        await fieldScope.CanAccessCycleAsync(cycleId, cancellationToken)
            ? Result.Success()
            : Result.Failure(CycleErrors.NotFound(cycleId));
}

/// <summary>
/// SQL conditions for the PPL scope in Dapper queries. The query must pass <c>@FieldRestricted</c> and
/// <c>@FieldUserId</c> (<see cref="FieldScope.Restricted"/> and <see cref="FieldScope.UserId"/>).
/// </summary>
public static class FieldScopeSql
{
    /// <summary>
    /// Coop row with the given table alias.
    /// </summary>
    public static string Coop(string alias) =>
        $"(NOT @FieldRestricted OR {alias}.field_officer_user_id = @FieldUserId)";

    /// <summary>
    /// Row linked to a coop by the given column (cycles, recordings, …).
    /// </summary>
    public static string CoopId(string coopIdColumn) =>
        $"""
         (NOT @FieldRestricted OR EXISTS (SELECT 1 FROM master.coops fs_c
                                          WHERE fs_c.id = {coopIdColumn} AND fs_c.field_officer_user_id = @FieldUserId))
         """;

    /// <summary>
    /// Farmer row with the given table alias: assigned directly or through one of its coops.
    /// </summary>
    public static string Farmer(string alias) =>
        $"""
         (NOT @FieldRestricted OR {alias}.field_officer_user_id = @FieldUserId
              OR EXISTS (SELECT 1 FROM master.coops fs_c
                         WHERE fs_c.farmer_id = {alias}.id AND fs_c.field_officer_user_id = @FieldUserId))
         """;
}
