using GmpPlatform.Core.Tickets;
using Xunit;

namespace GmpPlatform.Core.Tests;

public class SheetRolesTests
{
    // The approved AFIP-1035 seed: three layouts, each with viewports.
    private static readonly SheetCandidate[] Afip1035 =
    {
        new("1 of 3", 1, true, 4),
        new("2 of 3", 2, true, 9),
        new("3 of 3", 3, true, 10),
    };

    [Fact]
    public void Approved_seed_gets_tif_bif_sections_in_tab_order()
    {
        var roles = SheetRoles.Assign(Afip1035);

        Assert.Equal("1 of 3", roles.Tif);
        Assert.Equal("2 of 3", roles.Bif);
        Assert.Equal("3 of 3", roles.Sections);
        Assert.Empty(roles.Warnings);
    }

    /// <summary>
    /// Alphabetical order put "BIF" before "TIF" and "Sheet 10" before "Sheet 2". Tab order is
    /// what the drafter arranged.
    /// </summary>
    [Fact]
    public void Tab_order_wins_over_alphabetical_order()
    {
        var layouts = new[]
        {
            new SheetCandidate("SECTIONS", 3, true, 6),
            new SheetCandidate("BIF", 2, true, 1),
            new SheetCandidate("TIF", 1, true, 1),
        };

        var roles = SheetRoles.Assign(layouts);

        Assert.Equal("TIF", roles.Tif);
        Assert.Equal("BIF", roles.Bif);
        Assert.Equal("SECTIONS", roles.Sections);
    }

    [Fact]
    public void Layouts_without_viewports_are_skipped()
    {
        var layouts = new[]
        {
            new SheetCandidate("Cover", 1, false, 0),
            new SheetCandidate("Sheet 2", 2, true, 1),
            new SheetCandidate("Sheet 10", 3, true, 1),
            new SheetCandidate("Sheet 11", 4, true, 6),
        };

        var roles = SheetRoles.Assign(layouts);

        Assert.Equal("Sheet 2", roles.Tif);
        Assert.Equal("Sheet 10", roles.Bif);
        Assert.Equal("Sheet 11", roles.Sections);
    }

    [Fact]
    public void Configured_names_win_and_the_rest_follow_tab_order()
    {
        var roles = SheetRoles.Assign(Afip1035, tifLayout: "2 OF 3", bifLayout: null, sectionsLayout: " 3 of 3 ");

        Assert.Equal("2 of 3", roles.Tif);
        Assert.Equal("1 of 3", roles.Bif);
        Assert.Equal("3 of 3", roles.Sections);
        Assert.Empty(roles.Warnings);
    }

    [Fact]
    public void A_misconfigured_name_falls_back_and_is_reported()
    {
        var roles = SheetRoles.Assign(Afip1035, tifLayout: "Sheet 1");

        Assert.Equal("1 of 3", roles.Tif);
        Assert.Single(roles.Warnings);
        Assert.Contains("Sheet 1", roles.Warnings[0]);
    }

    [Fact]
    public void One_layout_is_never_given_two_roles()
    {
        var roles = SheetRoles.Assign(Afip1035, tifLayout: "1 of 3", bifLayout: "1 of 3");

        Assert.Equal("1 of 3", roles.Tif);
        Assert.Equal("2 of 3", roles.Bif);
        Assert.Single(roles.Warnings);
    }

    [Fact]
    public void A_seed_with_one_layout_gets_only_a_tif_view()
    {
        var roles = SheetRoles.Assign(new[] { new SheetCandidate("Layout1", 1, true, 1) });

        Assert.Equal("Layout1", roles.Tif);
        Assert.Null(roles.Bif);
        Assert.Null(roles.Sections);
    }
}
