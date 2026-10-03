using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Access.BranchAccessProfiles;
using Application.Access.MenuAccessProfiles;
using Application.Branches;
using Application.Branches.Get;
using Application.Roles.Get;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;

namespace Web.App.Areas.Admin.Controllers;

/// <summary>
/// Dropdown data shared by the administration screens.
/// </summary>
public sealed class AdminLookups(
    IQueryHandler<GetMenuAccessProfilesQuery, PagedList<MenuAccessProfileListItem>> menuProfilesQuery,
    IQueryHandler<GetBranchAccessProfilesQuery, PagedList<BranchAccessProfileListItem>> branchProfilesQuery,
    IQueryHandler<GetBranchAccessProfileByIdQuery, BranchAccessProfileResponse> branchProfileQuery,
    IQueryHandler<GetBranchesQuery, PagedList<BranchResponse>> branchesQuery,
    IQueryHandler<GetRolesQuery, IReadOnlyList<RoleResponse>> rolesQuery)
{
    private static readonly PageRequest All = new(1, PageRequest.MaxPageSize);

    public async Task<IReadOnlyList<SelectListItem>> MenuProfilesAsync(Guid? selected, CancellationToken cancellationToken)
    {
        Result<PagedList<MenuAccessProfileListItem>> result = await menuProfilesQuery.Handle(new GetMenuAccessProfilesQuery(All), cancellationToken);

        return result.IsFailure
            ? []
            : [.. result.Value.Items.Select(p => new SelectListItem(p.Name, p.Id.ToString(), p.Id == selected))];
    }

    public async Task<IReadOnlyList<SelectListItem>> BranchProfilesAsync(Guid? selected, CancellationToken cancellationToken)
    {
        Result<PagedList<BranchAccessProfileListItem>> result = await branchProfilesQuery.Handle(new GetBranchAccessProfilesQuery(All), cancellationToken);

        return result.IsFailure
            ? []
            : [.. result.Value.Items.Select(p => new SelectListItem(p.Name, p.Id.ToString(), p.Id == selected))];
    }

    /// <summary>
    /// Active branches covered by a branch access profile (every active branch for an all-branches profile).
    /// </summary>
    public async Task<IReadOnlyList<SelectListItem>> ProfileBranchesAsync(Guid? profileId, Guid? selected, CancellationToken cancellationToken)
    {
        if (profileId is not Guid id)
        {
            return [];
        }

        Result<BranchAccessProfileResponse> profile = await branchProfileQuery.Handle(new GetBranchAccessProfileByIdQuery(id), cancellationToken);
        if (profile.IsFailure)
        {
            return [];
        }

        IEnumerable<(Guid Id, string Code, string Name)> branches = profile.Value.AllBranches
            ? (await ActiveBranchesAsync(cancellationToken)).Select(b => (b.Id, b.Code, b.Name))
            : profile.Value.Branches.Where(b => b.IsActive).Select(b => (b.Id, b.Code, b.Name));

        return [.. branches.Select(b => new SelectListItem($"{b.Code} — {b.Name}", b.Id.ToString(), b.Id == selected))];
    }

    public async Task<IReadOnlyList<BranchResponse>> ActiveBranchesAsync(CancellationToken cancellationToken)
    {
        Result<PagedList<BranchResponse>> result = await branchesQuery.Handle(new GetBranchesQuery(All), cancellationToken);

        return result.IsFailure ? [] : [.. result.Value.Items.Where(b => b.IsActive)];
    }

    public async Task<IReadOnlyList<BranchResponse>> AllBranchesAsync(CancellationToken cancellationToken)
    {
        Result<PagedList<BranchResponse>> result = await branchesQuery.Handle(new GetBranchesQuery(All), cancellationToken);

        return result.IsFailure ? [] : result.Value.Items;
    }

    public async Task<IReadOnlyList<RoleResponse>> RolesAsync(CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<RoleResponse>> result = await rolesQuery.Handle(new GetRolesQuery(), cancellationToken);

        return result.IsFailure ? [] : result.Value;
    }
}
