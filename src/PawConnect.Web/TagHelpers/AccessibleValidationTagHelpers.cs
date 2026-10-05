using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PawConnect.Web.TagHelpers;

/// <summary>
/// Accessible form errors (WCAG 3.3.1 / 4.1.2). When the server rejects a field, its input gets
/// aria-invalid="true" and aria-describedby pointing at the error message, so screen readers
/// announce the error together with the field. Works for asp-for inputs and hand-written ones.
/// </summary>
[HtmlTargetElement("input", Attributes = "name")]
[HtmlTargetElement("input", Attributes = "asp-for")]
[HtmlTargetElement("select", Attributes = "name")]
[HtmlTargetElement("select", Attributes = "asp-for")]
[HtmlTargetElement("textarea", Attributes = "name")]
[HtmlTargetElement("textarea", Attributes = "asp-for")]
public class AriaInvalidTagHelper : TagHelper
{
    // Run after the built-in asp-for helpers, which write the name attribute.
    public override int Order => 1000;

    [ViewContext, HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!output.Attributes.TryGetAttribute("name", out var nameAttr)) return;
        var name = nameAttr.Value?.ToString();
        if (string.IsNullOrEmpty(name)) return;

        var state = ViewContext.ViewData.ModelState;
        if (state.TryGetValue(name, out var entry) && entry.ValidationState == ModelValidationState.Invalid)
        {
            output.Attributes.SetAttribute("aria-invalid", "true");
            output.Attributes.SetAttribute("aria-describedby", ValidationIds.For(name));
        }
    }
}

/// <summary>Gives each field's error message a stable id that the field can point to.</summary>
[HtmlTargetElement("span", Attributes = "asp-validation-for")]
public class ValidationMessageIdTagHelper : TagHelper
{
    public override int Order => 1000;

    [HtmlAttributeName("asp-validation-for")]
    public ModelExpression For { get; set; } = default!;

    [ViewContext, HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var name = ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(For.Name);
        output.Attributes.SetAttribute("id", ValidationIds.For(name));
    }
}

public static class ValidationIds
{
    public static string For(string fieldName) => "err-" + fieldName.Replace('.', '-').Replace('[', '-').Replace(']', '-');
}
