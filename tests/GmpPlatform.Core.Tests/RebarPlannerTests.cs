using GmpPlatform.Core.Rebar;
using GmpPlatform.Core.Tickets;
using Xunit;

namespace GmpPlatform.Core.Tests;

/// <summary>
/// The rebar rule is pinned against the approved AFIP-1035 ticket: if a change to the rules
/// stops reproducing its bar numbers, these fail.
/// </summary>
public class RebarPlannerTests
{
    // AFIP-1035 as read from the model: DIM_LENGTH 46'-3", DIM_WIDTH 12'-1 3/4".
    private static readonly PieceDimensions Afip1035 = new() { LengthIn = 555.0, HeightIn = 145.75 };

    [Theory]
    [InlineData(5, 480, "RSX05480")]
    [InlineData(5, 97, "RSX05097")]
    [InlineData(3, 143, "RSX03143")]
    [InlineData(11, 96, "RSX11096")] // a type present in the Revit model
    public void Part_number_is_size_and_length_in_inches(int size, int length, string expected) =>
        Assert.Equal(expected, RebarPlanner.PartNumber(size, length));

    [Fact]
    public void Reproduces_the_spliced_longitudinal_bars_on_the_approved_ticket()
    {
        var groups = RebarPlanner.Plan(Afip1035);

        // Ticket: "(2) RSX05480 SPLICED W/(2) RSX05097 ... (MIN. LAP 2'-1")", 6 of each.
        var first = Assert.Single(groups, g => g.PartNumber == "RSX05480");
        var second = Assert.Single(groups, g => g.PartNumber == "RSX05097");

        Assert.Equal(6, first.Count);
        Assert.Equal(6, second.Count);
        Assert.Equal("RSX05097", first.SplicedWith);
        Assert.Equal("RSX05480", second.SplicedWith);
    }

    [Fact]
    public void Reproduces_the_transverse_bar_number_on_the_approved_ticket()
    {
        var transverse = Assert.Single(RebarPlanner.Plan(Afip1035), g => g.Direction == "transverse");

        // 145.75" less 1" side cover each side, floored: the ticket's RSX03143.
        Assert.Equal("RSX03143", transverse.PartNumber);
    }

    [Fact]
    public void Transverse_count_is_an_assumption_and_differs_from_the_ticket()
    {
        // Documented, deliberately: 24" o.c. is not derived from the ticket, which shows 38.
        // This test exists so nobody later "fixes" the spacing to hit 38 on one piece and
        // presents a tuned number as a general rule.
        var transverse = Assert.Single(RebarPlanner.Plan(Afip1035), g => g.Direction == "transverse");
        Assert.NotEqual(38, transverse.Count);
    }

    [Fact]
    public void Splice_overlaps_by_the_lap()
    {
        var groups = RebarPlanner.Plan(Afip1035);
        var first = groups.Single(g => g.PartNumber == "RSX05480");
        var second = groups.Single(g => g.PartNumber == "RSX05097");

        var firstEnd = first.StartIn + first.LengthIn;
        Assert.Equal(25.0, firstEnd - second.StartIn, precision: 6);

        // Together they cover the run between the end covers (1.5" each end).
        Assert.Equal(555.0 - 1.5, second.StartIn + second.LengthIn, precision: 6);
    }

    [Fact]
    public void Short_panel_needs_no_splice()
    {
        var groups = RebarPlanner.Plan(new PieceDimensions { LengthIn = 300, HeightIn = 120 });
        var longitudinal = Assert.Single(groups, g => g.Direction == "longitudinal");

        Assert.Equal("RSX05297", longitudinal.PartNumber); // 300 - 2 x 1.5
        Assert.Equal("", longitudinal.SplicedWith);
    }

    [Fact]
    public void Survives_the_floating_point_length_revit_actually_produces()
    {
        // Regression: DIM_LENGTH 46.25 ft x 12 arrives as 554.9999999999432, not 555.
        // A plain Math.Floor gave RSX05096 on the real run while this suite, fed a clean
        // 555, stayed green. Always test with the value the model really produces.
        var fromRevit = new PieceDimensions { LengthIn = 554.9999999999432, HeightIn = 145.75 };
        var groups = RebarPlanner.Plan(fromRevit);

        Assert.Contains(groups, g => g.PartNumber == "RSX05097");
        Assert.DoesNotContain(groups, g => g.PartNumber == "RSX05096");
    }

    [Fact]
    public void Nothing_is_planned_without_dimensions() =>
        Assert.Empty(RebarPlanner.Plan(new PieceDimensions()));
}
