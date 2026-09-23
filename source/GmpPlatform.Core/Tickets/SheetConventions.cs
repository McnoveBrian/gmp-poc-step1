namespace GmpPlatform.Core.Tickets;

/// <summary>
/// How the client lays a piece out on a sheet. Every number here was measured off the approved
/// AFIP-1035 ticket rather than chosen, and each is recorded with the evidence that fixed it.
/// <para>
/// These live in the contract project, not in the AutoCAD engine, for two reasons: they
/// describe the client's drafting convention rather than anything about AutoCAD, and they are
/// the rules most likely to be wrong — so they need to be reachable by tests. Every one is a
/// single-sample measurement until a second approved ticket confirms it.
/// </para>
/// </summary>
public static class SheetConventions
{
    /// <summary>
    /// Width kept clear to the right of the piece for the stub-dimension column, in inches.
    /// The approved sheet centres its piece at x 253.1 in a window centred on 318.9 — a 65.8"
    /// shift left, which is half of this. Expressed as a reserved band rather than a shift so
    /// a longer piece still composes sensibly in what remains.
    /// </summary>
    public const double RightColumnReserve = 131.6;

    /// <summary>
    /// How far right of its station a callout's text sits. The drafter sets the label beside
    /// the dimension that locates it, not on top of it. With the piece correctly placed, all
    /// four <c>INS087</c> callouts came out a constant 20.9" left of the approved's, with no
    /// scatter — the signature of one missing offset rather than a wrong rule.
    /// </summary>
    public const double CalloutStationOffset = 20.9;

    /// <summary>Piece edge to the callout band's baseline.</summary>
    public const double CalloutBandGap = 8.0;

    /// <summary>First stub-dimension chain, measured beyond the callout band.</summary>
    public const double ChainBandBase = 20.0;

    /// <summary>Spacing between stacked dimension chains.</summary>
    public const double ChainBandStep = 28.0;

    /// <summary>
    /// An item within this distance of an edge is annotated off that edge. AFIP-1035's eight
    /// <c>INS087</c> recesses are four stations with one at the top edge (y ≈ 145.8) and one
    /// at the bottom (y ≈ 0.0); the ticket carries four callouts above the piece and four
    /// below.
    /// </summary>
    public const double EdgeBandTolerance = 6.0;

    /// <summary>Piece bottom down to the SIF edge view.</summary>
    public const double SifOffset = 95.0;

    /// <summary>SIF edge view down to its dimension line.</summary>
    public const double SifDimOffset = 26.0;

    /// <summary>
    /// Turns a panel station — the contract's X, zero at the START PANEL end — into a drawing
    /// coordinate. **Stations run right to left**, which is what the approved ticket's
    /// "◄— DIRECTION OF STUB DIMS." states.
    /// <para>
    /// Measured, not assumed. AFIP-1035's four <c>INS087</c> recesses are at stations 30, 171,
    /// 342 and 498 — the same values the approved drawing dimensions, so both measure from the
    /// same physical end — yet their callouts sit at drawing x 521.5, 380.5, 209.5 and 53.5.
    /// Fitting <c>x = C - station</c> reproduces all four exactly; <c>x = C + station</c> is
    /// 15" out on the middle two. Drawing stations left to right produces a mirror image of
    /// the client's sheet: every value correct and the piece the wrong way round, which on a
    /// shop ticket is how a panel gets cast backwards.
    /// </para>
    /// </summary>
    public static double StationToDrawingX(double originX, double pieceLengthIn, double station) =>
        originX + pieceLengthIn - station;

    /// <summary>
    /// Left edge of the piece within its view window. The piece is not centred: the approved
    /// sheet keeps <see cref="RightColumnReserve"/> clear on the right for the dimension
    /// column that reads down the side of the piece.
    /// </summary>
    public static double PieceOriginX(double windowCenterX, double pieceLengthIn) =>
        windowCenterX - RightColumnReserve / 2.0 - pieceLengthIn / 2.0;

    /// <summary>Whether an item at this height is annotated off the bottom edge.</summary>
    public static bool IsBottomEdge(double yIn) => yIn <= EdgeBandTolerance;

    /// <summary>
    /// How far a rotated callout reaches out of its baseline, in inches, so the dimension
    /// chains can start beyond it. Text is measured rather than guessed at: the longest
    /// callout string decides how much room the band needs.
    /// </summary>
    public static double CalloutRun(double annotationScale)
    {
        const int longestCallout = 22;   // "℄ (2) PAS018 (TIF & BIF)"
        var textHeight = 3.5 * annotationScale / 64.0;
        return longestCallout * textHeight * 0.62;
    }
}
