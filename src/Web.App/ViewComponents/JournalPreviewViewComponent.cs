using Application.Abstractions.Messaging;
using Application.Finance.Journals;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Infrastructure.Authorization;

namespace Web.App.ViewComponents;

/// <param name="Journals">The document's journals (automatic journals are posted by the background job).</param>
public sealed record JournalPreviewModel(IReadOnlyList<JournalResponse> Journals, string EmptyText);

/// <summary>
/// "Journal" card on a business document's detail page (PLAN-WEBAPP §21.2): the journals whose source is the document,
/// with their lines and a link to the journal. Rendered only for users who may view journals.
/// </summary>
public sealed class JournalPreviewViewComponent(
    IMenuRights menuRights,
    IQueryHandler<GetDocumentJournalsQuery, IReadOnlyList<JournalResponse>> journalsQuery) : ViewComponent
{
    /// <param name="sourceIds">The document id (plus related ids, e.g. advance applications of a receipt).</param>
    /// <param name="emptyText">Shown when no journal exists yet (e.g. a draft document).</param>
    public async Task<IViewComponentResult> InvokeAsync(IEnumerable<Guid> sourceIds, string? emptyText = null)
    {
        if (!await menuRights.CanAsync(MenuCodes.FinanceJournals, MenuRights.View))
        {
            return Content(string.Empty);
        }

        Result<IReadOnlyList<JournalResponse>> result = await journalsQuery.Handle(
            new GetDocumentJournalsQuery([.. sourceIds.Distinct()]), HttpContext.RequestAborted);

        return View(new JournalPreviewModel(
            result.IsSuccess ? result.Value : [],
            emptyText ?? "No journal yet — it is created when the document is posted (automatic journals may take a few seconds)."));
    }
}
