using topg.Web.Client.Creator.Model;

namespace topg.Web.Client.Creator.Validation;

/// <summary>
/// Field validators for MudBlazor's <c>Validation</c> parameter. They mirror <see cref="ProjectValidator"/> so a field
/// shows the problem while typing (MudBlazor's own validation would otherwise reset an <c>Error</c> parameter).
/// </summary>
public static class CreatorValidation
{
    public static string? Folder(string? value) =>
        Slug.IsValid(value?.Trim()) ? null : "Only lowercase letters, digits and dashes.";

    public static string? BaseUrl(string? value) =>
        ProjectValidator.IsAbsoluteHttpUrl(value) ? null : "Must be an absolute http(s) URL.";
}
