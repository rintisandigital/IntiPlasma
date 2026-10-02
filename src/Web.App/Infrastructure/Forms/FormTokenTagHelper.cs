using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Web.App.Infrastructure.Forms;

/// <summary>
/// <c>&lt;form-token /&gt;</c> renders the hidden one-time token checked by <see cref="FormTokenFilter"/>.
/// The token is a Guid v7, so create use cases that accept a client id may reuse it as the document id.
/// </summary>
[HtmlTargetElement("form-token", TagStructure = TagStructure.WithoutEndTag)]
public sealed class FormTokenTagHelper : TagHelper
{
    /// <summary>
    /// Keeps the token of a re-rendered form (validation errors), so a later successful post is still guarded.
    /// </summary>
    public Guid? Value { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        output.TagName = "input";
        output.TagMode = TagMode.SelfClosing;
        output.Attributes.SetAttribute("type", "hidden");
        output.Attributes.SetAttribute("name", FormTokenFilter.FieldName);
        output.Attributes.SetAttribute("value", (Value ?? Guid.CreateVersion7()).ToString());
    }
}
