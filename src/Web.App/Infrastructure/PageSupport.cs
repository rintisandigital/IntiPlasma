using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Documents;
using Application.Users.FieldOfficers;
using Application.Users.GetCurrent;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;
using Web.App.Infrastructure.Auth;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;

namespace Web.App.Infrastructure;

/// <summary>
/// Services most list/form screens need: rights, the branch filter default, attachments and exports.
/// </summary>
public sealed class PageSupport(
    IMenuRights menuRights,
    IBranchContext branchContext,
    IAttachmentService attachments,
    IQueryHandler<GetFieldOfficersQuery, IReadOnlyList<FieldOfficerResponse>> fieldOfficers,
    ExportService exports)
{
    /// <summary>
    /// Value of the branch filter meaning "every branch I may see".
    /// </summary>
    public const string AllBranches = "all";

    public ExportService Exports => exports;

    public Task<bool> CanAsync(string menuCode, MenuRights right) => menuRights.CanAsync(menuCode, right);

    /// <summary>
    /// Branch filter of a list: an explicit branch, "all", or (nothing chosen) the branch selected in the header.
    /// </summary>
    public async Task<BranchFilter> BranchFilterAsync(string? branch)
    {
        CurrentUserResponse? user = await branchContext.GetUserAsync();
        IReadOnlyList<CurrentUserBranch> branches = user?.Branches ?? [];

        Guid? branchId = string.Equals(branch, AllBranches, StringComparison.Ordinal) ? null : await ExplicitOrActiveAsync(branch);

        List<SelectListItem> options = [new("All branches", AllBranches, branchId is null)];
        options.AddRange(ToOptions(branches, branchId));

        return new BranchFilter(branchId, options, branches.FirstOrDefault(b => b.Id == branchId)?.Name);
    }

    /// <summary>
    /// The user's branches as dropdown options (forms of branch-owned records).
    /// </summary>
    public async Task<IReadOnlyList<SelectListItem>> BranchOptionsAsync(Guid? selected)
    {
        CurrentUserResponse? user = await branchContext.GetUserAsync();

        selected ??= (await branchContext.GetActiveBranchAsync())?.Id;

        return [.. ToOptions(user?.Branches ?? [], selected)];
    }

    /// <summary>
    /// PPL filter of a list (PLAN-MOBILE M1): the field officers of the branch, or of every branch I may see.
    /// </summary>
    public async Task<IReadOnlyList<SelectListItem>> FieldOfficerOptionsAsync(
        Guid? branchId,
        Guid? selected,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<FieldOfficerResponse>> result =
            await fieldOfficers.Handle(new GetFieldOfficersQuery(branchId), cancellationToken);

        return result.IsFailure
            ? []
            : [.. result.Value.Select(o => new SelectListItem(o.Name, o.Id.ToString(), o.Id == selected))];
    }

    /// <summary>
    /// Label of the chosen PPL when a form is shown again after a failed save.
    /// </summary>
    public async Task<string?> FieldOfficerLabelAsync(Guid? branchId, Guid? fieldOfficerUserId, CancellationToken cancellationToken)
    {
        if (branchId is null || fieldOfficerUserId is null)
        {
            return null;
        }

        return (await FieldOfficerOptionsAsync(branchId, null, cancellationToken))
            .FirstOrDefault(o => o.Value == fieldOfficerUserId.ToString())?.Text;
    }

    public async Task<IReadOnlyList<AttachmentResponse>> AttachmentsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken) =>
        ids.Count == 0 ? [] : await attachments.GetManyAsync(ids, cancellationToken);

    /// <summary>
    /// Runs a paged list query for every page and returns the file, or an error message for the user.
    /// </summary>
    public async Task<IActionResult> ExportAsync<T>(
        string? format,
        string title,
        string fileName,
        IReadOnlyList<string> filters,
        IReadOnlyList<ExportColumn<T>> columns,
        Func<PageRequest, CancellationToken, Task<Result<PagedList<T>>>> fetch,
        string? search,
        CancellationToken cancellationToken)
    {
        if (!ExportFileFormats.TryParse(format, out ExportFileFormat fileFormat))
        {
            return new BadRequestObjectResult("Unknown export format.");
        }

        Result<IReadOnlyList<T>> rows = await PagedExportRunner.CollectAsync(fetch, search, exports.MaxRows, cancellationToken);

        if (rows.IsFailure)
        {
            return new ContentResult { Content = rows.Error.Description, ContentType = "text/plain", StatusCode = StatusCodes.Status400BadRequest };
        }

        ExportHeader header = await exports.HeaderAsync(title, fileName, filters);

        return exports.List(fileFormat, header, columns, rows.Value);
    }

    /// <summary>
    /// Same as <see cref="ExportAsync{T}"/> for lists that are not paged.
    /// </summary>
    public async Task<IActionResult> ExportAsync<T>(
        string? format,
        string title,
        string fileName,
        IReadOnlyList<string> filters,
        IReadOnlyList<ExportColumn<T>> columns,
        IReadOnlyList<T> rows)
    {
        if (!ExportFileFormats.TryParse(format, out ExportFileFormat fileFormat))
        {
            return new BadRequestObjectResult("Unknown export format.");
        }

        ExportHeader header = await exports.HeaderAsync(title, fileName, filters);

        return exports.List(fileFormat, header, columns, rows);
    }

    private async Task<Guid?> ExplicitOrActiveAsync(string? branch) =>
        Guid.TryParse(branch, out Guid parsed) ? parsed : (await branchContext.GetActiveBranchAsync())?.Id;

    private static IEnumerable<SelectListItem> ToOptions(IEnumerable<CurrentUserBranch> branches, Guid? selected) =>
        branches.Select(b => new SelectListItem($"{b.Code} — {b.Name}", b.Id.ToString(), b.Id == selected));
}

/// <param name="BranchId">Null for every accessible branch.</param>
/// <param name="BranchName">Name for export titles (null for all branches).</param>
public sealed record BranchFilter(Guid? BranchId, IReadOnlyList<SelectListItem> Options, string? BranchName)
{
    public string Description => $"Branch: {BranchName ?? "All branches"}";
}
