using Application.Abstractions.Messaging;
using Application.Users.FieldOfficers;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Partnership.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;

namespace Web.App.Areas.Partnership.Controllers;

/// <summary>
/// Field Officer Assignment (PLAN-MOBILE M-39): moves the farmers and farms of one PPL (or the unassigned ones) to
/// another PPL at once, e.g. when an employee moves. The mobile app limits a PPL to their assigned data.
/// </summary>
[Area("Partnership")]
[MenuAccess(MenuCodes.PartnershipFieldOfficers)]
public sealed class FieldOfficersController(
    PageSupport support,
    IQueryHandler<GetFieldOfficerAssignmentsQuery, FieldOfficerAssignmentsResponse> assignmentsQuery) : AppController
{
    private const string MenuCode = MenuCodes.PartnershipFieldOfficers;

    [HttpGet]
    public async Task<IActionResult> Index(Guid? branchId, string? from, CancellationToken cancellationToken)
    {
        var branches = (await support.BranchOptionsAsync(branchId)).ToList();
        Guid? branch = Guid.TryParse(branches.FirstOrDefault(b => b.Selected)?.Value ?? branches.FirstOrDefault()?.Value, out Guid id)
            ? id
            : null;

        var model = new FieldOfficerAssignmentViewModel
        {
            BranchId = branch,
            From = from,
            Branches = branches,
            FieldOfficers = branch is null ? [] : await support.FieldOfficerOptionsAsync(branch, null, cancellationToken),
            CanMove = await support.CanAsync(MenuCode, MenuRights.Edit)
        };

        if (branch is not null && from is not null)
        {
            Result<FieldOfficerAssignmentsResponse> result = await assignmentsQuery.Handle(
                new GetFieldOfficerAssignmentsQuery(branch.Value, ParseOfficer(from)), cancellationToken);

            if (result.IsFailure)
            {
                return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
            }

            model.Assignments = result.Value;
        }

        return View(model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Move(
        Guid branchId,
        string from,
        string? to,
        Guid[]? farmerIds,
        Guid[]? coopIds,
        [FromServices] ICommandHandler<ReassignFieldOfficerCommand, int> handler,
        CancellationToken cancellationToken)
    {
        Result<int> result = await handler.Handle(
            new ReassignFieldOfficerCommand(branchId, ParseOfficer(from), ParseOfficer(to), farmerIds ?? [], coopIds ?? []),
            cancellationToken);

        if (result.IsSuccess)
        {
            NotifySuccess(result.Value == 0
                ? "Nothing was moved; the selection was already changed by someone else."
                : $"{result.Value} record(s) have been moved to the new field officer.");
        }
        else
        {
            NotifyError(result.Error.Description);
        }

        return RedirectToAction(nameof(Index), new { branchId, from });
    }

    /// <summary>
    /// <see cref="FieldOfficerAssignmentViewModel.Unassigned"/> (or nothing) means no PPL.
    /// </summary>
    private static Guid? ParseOfficer(string? value) => Guid.TryParse(value, out Guid id) ? id : null;
}
