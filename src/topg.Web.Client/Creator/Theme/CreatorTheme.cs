using MudBlazor;

namespace topg.Web.Client.Creator.Theme;

/// <summary>
/// Theme of the quiz creator (UX-3). Uses the same palettes as the game's MainLayout, but denser
/// shapes: the creator is a workbench, not a stage.
/// </summary>
public static class CreatorTheme
{
    public static MudTheme Create() => new()
    {
        // The light app bar is a plain surface (like the dark one), so the section links stay readable.
        PaletteLight = new PaletteLight
        {
            AppbarBackground = "#ffffff",
            AppbarText = "rgba(0, 0, 0, 0.87)",
        },
        PaletteDark = new PaletteDark(),
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "6px",
        },
    };
}
