using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PawConnect.Web.TagHelpers;

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
