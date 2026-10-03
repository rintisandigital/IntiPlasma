using Domain.Access;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Web.App.Infrastructure.Authorization;

/// <summary>
/// Renders the element only when the user has the right: <c>&lt;a asp-menu="admin.users" asp-right="Create"&gt;</c>.
/// Hiding is a convenience; the target action is protected by <see cref="MenuAccessAttribute"/>.
/// </summary>
[HtmlTargetElement("a", Attributes = MenuAttribute)]
[HtmlTargetElement("button", Attributes = MenuAttribute)]
[HtmlTargetElement("form", Attributes = MenuAttribute)]
[HtmlTargetElement("li", Attributes = MenuAttribute)]
[HtmlTargetElement("ul", Attributes = MenuAttribute)]
[HtmlTargetElement("div", Attributes = MenuAttribute)]
public sealed class MenuRightTagHelper(IMenuRights menuRights) : TagHelper
{
    private const string MenuAttribute = "asp-menu";

    /// <summary>
    /// Runs before the other tag helpers so suppressed elements are not processed further.
    /// </summary>
    public override int Order => -1000;

    [HtmlAttributeName(MenuAttribute)]
    public string Menu { get; set; } = string.Empty;

    [HtmlAttributeName("asp-right")]
    public MenuRights Right { get; set; } = MenuRights.View;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (!await menuRights.CanAsync(Menu, Right))
        {
            output.SuppressOutput();
        }
    }
}
