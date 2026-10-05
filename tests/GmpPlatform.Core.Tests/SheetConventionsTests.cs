using GmpPlatform.Core.Tickets;
using Xunit;

namespace GmpPlatform.Core.Tests;

/// <summary>
/// Locks the drafting conventions measured from the approved AFIP-1035 ticket.
/// <para>
/// These are the rules most likely to be got wrong and least likely to be noticed: the engine
/// shipped a mirrored sheet for several increments with every number on it correct. The
/// numbers below are the client's own, read off their drawing, so a change that breaks one of
/// these tests is a change that puts the piece back the wrong way round.
/// </para>
/// </summary>
public class SheetConventionsTests
{
    private const double PieceLength = 555.0;   // AFIP-1035, 46'-3"

    [Fact]
    public void Stations_run_right_to_left()
    {
        // Station 0 is the START PANEL end and belongs at the right of the sheet.
        var origin = 0.0;
        var atZero = SheetConventions.StationToDrawingX(origin, PieceLength, 0);
        var atFar = SheetConventions.StationToDrawingX(origin, PieceLength, PieceLength);

        Assert.True(atZero > atFar,
            "station 0 must draw to the right of the far end; drawing left-to-right mirrors the sheet");
        Assert.Equal(PieceLength, atZero, 6);
        Assert.Equal(0.0, atFar, 6);
    }

    [Theory]
    // The four INS087 recesses and where the approved ticket's callouts sit, relative to the
    // piece. Spacings 156, 171 and 141 are the drawing's own and must survive any refactor.
    [InlineData(30, 525)]
    [InlineData(171, 384)]
    [InlineData(342, 213)]
    [InlineData(498, 57)]
    public void Ins087_stations_map_to_the_drafters_positions(double station, double expectedFromLeft)
    {
        var x = SheetConventions.StationToDrawingX(0, PieceLength, station);
        Assert.Equal(expectedFromLeft, x, 6);
    }

    [Fact]
    public void Ins087_spacings_match_the_approved_sheet()
    {
        double[] stations = { 30, 171, 342, 498 };
        var xs = stations.Select(s => SheetConventions.StationToDrawingX(0, PieceLength, s))
                         .OrderBy(v => v)
                         .ToArray();

        // Measured off the approved drawing: 53.5, 209.5, 380.5, 521.5.
        Assert.Equal(156.0, xs[1] - xs[0], 6);
        Assert.Equal(171.0, xs[2] - xs[1], 6);
        Assert.Equal(141.0, xs[3] - xs[2], 6);
    }

    /// <summary>
    /// Section letters read left to right across the TIF view. Ordering by station did the
    /// opposite once stations ran right to left: "A" landed at the right-hand end, against the
    /// reading direction of everything else on the sheet.
    /// </summary>
    [Fact]
    public void Sheet_reading_order_runs_left_to_right()
    {
        double[] stations = { 30, 498, 171, 342 };
        var ordered = SheetConventions.InSheetReadingOrder(stations, s => s, PieceLength).ToArray();

        Assert.Equal(new double[] { 498, 342, 171, 30 }, ordered);

        var xs = ordered.Select(s => SheetConventions.StationToDrawingX(0, PieceLength, s)).ToArray();
        Assert.Equal(xs.OrderBy(x => x), xs);
    }

    [Fact]
    public void Piece_is_not_centred_in_its_window()
    {
        // Sheet 1's window centre is 318.9; the approved sheet puts the piece's left edge at
        // -24.4, keeping a band clear on the right for the stub-dimension column.
        var originX = SheetConventions.PieceOriginX(318.9, PieceLength);
        Assert.Equal(-24.4, originX, 1);
    }

    [Fact]
    public void Piece_is_shifted_left_by_half_the_reserve()
    {
        const double windowCentre = 318.9;
        var pieceCentre = SheetConventions.PieceOriginX(windowCentre, PieceLength) + PieceLength / 2.0;

        // The reserve shifts the piece off-centre; it does not clear a band wider than the
        // piece. A longer piece still overhangs the window centre, which is why the fit
        // calculation adds the reserve to the required width rather than assuming it fits.
        Assert.Equal(SheetConventions.RightColumnReserve / 2.0, windowCentre - pieceCentre, 6);
    }

    [Fact]
    public void Shift_is_independent_of_piece_length()
    {
        const double windowCentre = 318.9;
        foreach (var length in new[] { 46.125, 555.0, 683.0 })
        {
            var centre = SheetConventions.PieceOriginX(windowCentre, length) + length / 2.0;
            Assert.Equal(SheetConventions.RightColumnReserve / 2.0, windowCentre - centre, 6);
        }
    }

    [Theory]
    [InlineData(0.0, true)]      // INS087 at the bottom edge
    [InlineData(6.0, true)]      // exactly on the tolerance
    [InlineData(6.01, false)]
    [InlineData(37.8, false)]    // PAS018, interior
    [InlineData(145.8, false)]   // INS087 at the top edge
    public void Bottom_edge_items_are_annotated_below_the_piece(double y, bool expected)
    {
        Assert.Equal(expected, SheetConventions.IsBottomEdge(y));
    }

    [Fact]
    public void Callout_run_scales_with_the_view()
    {
        // The band has to grow with the annotation scale or the chains collide with the text.
        var at64 = SheetConventions.CalloutRun(64);
        var at96 = SheetConventions.CalloutRun(96);

        Assert.True(at64 > 0);
        Assert.Equal(at64 * 1.5, at96, 6);
    }
}
