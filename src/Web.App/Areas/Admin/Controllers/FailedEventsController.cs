using Application.Abstractions.Messaging;
using Application.Monitoring;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;

namespace Web.App.Areas.Admin.Controllers;

/// <summary>
/// Domain events that failed after all retries (dead letters), e.g. an automatic journal rejected because a mapping is
/// missing or the fiscal period does not exist. They block closing the period; once the cause is fixed they are put
/// back in the queue and processed by the background job.
/// </summary>
[Area("Admin")]
[MenuAccess(MenuCodes.AdminFailedEvents)]
public sealed class FailedEventsController(
    PageSupport support,
    IQueryHandler<GetFailedEventsQuery, IReadOnlyList<FailedEventResponse>> eventsQuery) : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<FailedEventResponse>> result = await eventsQuery.Handle(new GetFailedEventsQuery(), cancellationToken);

        ViewData["CanRetry"] = await support.CanAsync(MenuCodes.AdminFailedEvents, MenuRights.Edit);
        return View(result.IsSuccess ? result.Value : []);
    }

    /// <param name="id">One event; empty = every failed event.</param>
    [HttpPost]
    [MenuAccess(MenuCodes.AdminFailedEvents, MenuRights.Edit)]
    public async Task<IActionResult> Retry(
        Guid? id,
        [FromServices] ICommandHandler<RetryFailedEventsCommand, int> handler,
        CancellationToken cancellationToken)
    {
        Result<int> result = await handler.Handle(new RetryFailedEventsCommand(id), cancellationToken);

        if (result.IsSuccess)
        {
            NotifySuccess(result.Value == 1
                ? "The event has been queued again; the background job processes it within a few seconds."
                : $"{result.Value} events have been queued again; the background job processes them within a few seconds.");
        }
        else
        {
            NotifyError(result.Error.Description);
        }

        return RedirectToAction(nameof(Index));
    }
}
