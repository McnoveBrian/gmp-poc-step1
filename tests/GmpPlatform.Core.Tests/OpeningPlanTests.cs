using GmpPlatform.Core.Tickets;
using Xunit;

namespace GmpPlatform.Core.Tests;

public class OpeningPlanTests
{
    private static Point2 P(double x, double y) => new() { X = x, Y = y };

    private static List<Point2> Rect(double x0, double y0, double x1, double y1) =>
        new() { P(x0, y0), P(x1, y0), P(x1, y1), P(x0, y1) };

    /// <summary>
    /// AFIP-1035 as the approved ticket dimensions it: a 46'-3" x 12'-1½" panel with B.O.-1 cut
    /// from the bottom edge between 17'-7¾" and 22'-8¼", 3'-11⅞" high, and B.O.-2,3 stacked
    /// between 30'-8" and 32'-8".
    /// </summary>
    private static PieceGeometry Afip1035Like() => new()
    {
        Outline = new List<Point2>
        {
            P(0, 145.5), P(0, 0),
            P(211.75, 0), P(211.75, 47.875), P(272.25, 47.875), P(272.25, 0),
            P(555, 0), P(555, 145.5)
        },
        Openings = new List<List<Point2>>
        {
            Rect(368, 30, 392, 54),
            Rect(368, 70, 392, 94),
        }
    };

    [Fact]
    public void Bottom_notch_in_the_outline_is_an_opening_and_numbered_first()
    {
        var openings = OpeningPlan.Find(Afip1035Like());

        Assert.Equal(3, openings.Count);
        var bo1 = openings[0];
        Assert.Equal(1, bo1.Number);
        Assert.Equal(OpeningKind.BottomNotch, bo1.Kind);
        Assert.Equal(211.75, bo1.MinX);
        Assert.Equal(272.25, bo1.MaxX);
        Assert.Equal(0, bo1.MinY);
        Assert.Equal(47.875, bo1.MaxY);
    }

    [Fact]
    public void Stacked_openings_number_top_down_and_share_one_group()
    {
        var openings = OpeningPlan.Find(Afip1035Like());

        Assert.Equal(OpeningKind.Inner, openings[1].Kind);
        Assert.Equal(94, openings[1].MaxY);   // B.O.-2 is the upper one
        Assert.Equal(54, openings[2].MaxY);

        var groups = OpeningPlan.Group(openings, 145.5);
        Assert.Equal(2, groups.Count);
        Assert.Equal("1", groups[0].Numbers);
        Assert.Equal("2,3", groups[1].Numbers);
        Assert.Equal(368, groups[1].Start);
        Assert.Equal(392, groups[1].Stop);
        Assert.True(groups[0].Below);
        Assert.True(groups[1].Below);
    }

    /// <summary>
    /// FIP-1171's outline carries two 8" pockets cut from the STOP PANEL end. They are end
    /// details, not openings, and must not be dimensioned as blockouts.
    /// </summary>
    [Fact]
    public void End_pockets_are_not_openings()
    {
        var geometry = new PieceGeometry
        {
            Outline = new List<Point2>
            {
                P(0, 145.5), P(0, 0), P(574, 0), P(574, 25.757), P(566, 25.757), P(566, 34.243),
                P(574, 34.243), P(574, 111.257), P(566, 111.257), P(566, 119.743), P(574, 119.743), P(574, 145.5)
            }
        };

        Assert.Empty(OpeningPlan.Find(geometry));
    }

    [Fact]
    public void A_step_in_an_L_shaped_piece_is_not_a_notch()
    {
        var geometry = new PieceGeometry
        {
            Outline = new List<Point2> { P(0, 0), P(300, 0), P(300, 40), P(500, 40), P(500, 145), P(0, 145) }
        };

        Assert.Empty(OpeningPlan.Find(geometry));
    }

    [Fact]
    public void Top_notch_is_found_and_annotated_above()
    {
        var geometry = new PieceGeometry
        {
            Outline = new List<Point2>
            {
                P(0, 0), P(400, 0), P(400, 120), P(250, 120), P(250, 90), P(200, 90), P(200, 120), P(0, 120)
            }
        };

        var openings = OpeningPlan.Find(geometry);
        var notch = Assert.Single(openings);
        Assert.Equal(OpeningKind.TopNotch, notch.Kind);
        Assert.Equal(90, notch.MinY);
        Assert.Equal(120, notch.MaxY);

        Assert.False(Assert.Single(OpeningPlan.Group(openings, 120)).Below);
    }

    [Fact]
    public void Closed_loops_and_slivers_are_handled()
    {
        var closed = Rect(100, 50, 130, 80);
        closed.Add(P(100, 50));   // repeated closing vertex
        var geometry = new PieceGeometry
        {
            Outline = Rect(0, 0, 500, 145),
            Openings = new List<List<Point2>> { closed, Rect(200, 50, 200.01, 60) }
        };

        var opening = Assert.Single(OpeningPlan.Find(geometry));
        Assert.Equal(30, opening.Width);
        Assert.Equal(30, opening.Height);
    }

    [Fact]
    public void Vertical_spans_split_the_height_and_label_the_opening()
    {
        var groups = OpeningPlan.Group(OpeningPlan.Find(Afip1035Like()), 145.5);

        var bo1 = groups[0].VerticalSpans(145.5);
        Assert.Equal(2, bo1.Count);
        Assert.Equal((0.0, 47.875, (int?)1), (bo1[0].From, bo1[0].To, bo1[0].Opening));
        Assert.Equal((47.875, 145.5, (int?)null), (bo1[1].From, bo1[1].To, bo1[1].Opening));

        var stack = groups[1].VerticalSpans(145.5);
        Assert.Equal(new double[] { 0, 30, 54, 70, 94 }, stack.Select(s => s.From));
        Assert.Equal(new int?[] { null, 3, null, 2, null }, stack.Select(s => s.Opening));
    }
}
