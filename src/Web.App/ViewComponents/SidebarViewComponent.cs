using Microsoft.AspNetCore.Mvc;
using Web.App.Infrastructure.Navigation;

namespace Web.App.ViewComponents;

/// <summary>
/// The two-column sidebar of the mainboard: module icons on the left, the module's menu on the right.
/// Every link opens in the content iframe.
/// </summary>
public sealed class SidebarViewComponent(IMainboardMenuProvider menuProvider) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync() =>
        View(await menuProvider.GetMenuAsync(HttpContext.RequestAborted));
}
