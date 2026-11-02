using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PawConnect.Web.TagHelpers;

public static class ValidationIds
{
    public static string For(string fieldName) => "err-" + fieldName.Replace('.', '-').Replace('[', '-').Replace(']', '-');
}
