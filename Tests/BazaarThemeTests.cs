using kwikby_MP.Services;
using Xunit;

namespace kwikby_MP.Tests;

/// <summary>
/// The Bazaar skin is selectable, resolves by key, and is the only theme that uses the marketplace arrangement.
/// </summary>
public class BazaarThemeTests
{
    [Fact]
    public void Bazaar_is_a_selectable_theme()
    {
        var catalog = new ThemeCatalog();

        Assert.True(catalog.Contains("bazaar"));
        Assert.Equal("Bazaar", catalog.Get("bazaar").Name);
    }

    [Fact]
    public void Bazaar_uses_the_marketplace_arrangement_and_the_others_stay_classic()
    {
        var catalog = new ThemeCatalog();

        Assert.Equal("marketplace", catalog.Get("bazaar").Arrangement);
        foreach (var theme in catalog.All.Where(t => t.Key != "bazaar"))
        {
            Assert.Equal("classic", theme.Arrangement);
        }
    }

    [Fact]
    public void Bazaar_palette_is_written_as_brand_variables()
    {
        var css = new ThemeCatalog().Get("bazaar").ToCssVariables();

        Assert.Contains("--brand-primary:#0f766e", css);
        Assert.Contains("--brand-accent:#ff6b35", css);
    }
}
