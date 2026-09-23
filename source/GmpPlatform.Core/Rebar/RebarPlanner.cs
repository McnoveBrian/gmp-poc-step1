using GmpPlatform.Core.Tickets;

namespace GmpPlatform.Core.Rebar;

/// <summary>
/// The reinforcement rules, as far as they can be inferred. Every value here is an
/// assumption to be confirmed with GMP; the defaults are the ones that reproduce the
/// approved AFIP-1035 ticket.
/// </summary>
public sealed class RebarRules
{
    /// <summary>Cover at the two panel ends, where the bars run out along the length.</summary>
    public double EndCoverIn { get; set; } = 1.5;

    /// <summary>Cover at the panel edges the transverse bars run between.</summary>
    public double SideCoverIn { get; set; } = 1.0;

    /// <summary>Longest bar supplied; longer runs are spliced.</summary>
    public double MaxStockIn { get; set; } = 480;

    /// <summary>Splice lap. The ticket prints "MIN. LAP 2'-1&quot;".</summary>
    public double LapIn { get; set; } = 25;

    public int LongitudinalSize { get; set; } = 5;
    public int TransverseSize { get; set; } = 3;

    /// <summary>Longitudinal bar rows across the height, each placed in both wythes.</summary>
    public int LongitudinalRows { get; set; } = 3;

    /// <summary>
    /// Transverse spacing. NOT derived from the ticket — a common precast value used as a
    /// stated assumption. The ticket's own count (38) implies a different spacing or extra
    /// bars around openings; that is the question to put to GMP.
    /// </summary>
    public double TransverseSpacingIn { get; set; } = 24;

    /// <summary>Human-readable statement of the rules, for the drawing's derived note.</summary>
    public string Describe() =>
        $"rebar rule-generated: end cover {EndCoverIn:0.##}\", side cover {SideCoverIn:0.##}\", " +
        $"{MaxStockIn:0}\" stock, {LapIn:0}\" lap, #{TransverseSize} @ {TransverseSpacingIn:0}\" o.c. " +
        "- confirm spacing and trim bars with GMP";
}

/// <summary>A group of identical bars, with where they sit so they can be drawn.</summary>
public sealed class RebarGroup
{
    public string PartNumber { get; set; } = "";
    public int Size { get; set; }
    public int LengthIn { get; set; }
    public int Count { get; set; }

    /// <summary>"longitudinal" (along the panel length) or "transverse" (across the height).</summary>
    public string Direction { get; set; } = "";

    /// <summary>Start of each bar along its run direction, in panel-local inches.</summary>
    public double StartIn { get; set; }

    /// <summary>Position of each bar line across its run direction, in panel-local inches.</summary>
    public List<double> Offsets { get; set; } = new();

    /// <summary>Other part number this bar is spliced with, if any.</summary>
    public string SplicedWith { get; set; } = "";
}

/// <summary>
/// Rule-generated field reinforcement for a sandwich wall panel.
/// <para>
/// Part numbers follow the client's scheme — RSX + two-digit bar size + three-digit length in
/// inches — confirmed three ways: the arithmetic of the approved BOM, the RSX types present in
/// the Revit model (RSX05072, RSX05277, RSX11096), and the client's REBAR dynamic block, whose
/// parameters are bar size and length.
/// </para>
/// <para>
/// Only field bars are generated. Trim bars around openings and blockouts (RSX03023,
/// RSX03074, RSX04143, RSX05209, RSX05280, RSX05365 on AFIP-1035) are piece-specific and
/// need GMP's rules; they are not guessed.
/// </para>
/// </summary>
public static class RebarPlanner
{
    public static string PartNumber(int size, int lengthIn) => $"RSX{size:00}{lengthIn:000}";

    /// <summary>
    /// Whole inches, rounding down, tolerant of floating-point noise. Lengths arrive from
    /// Revit as feet x 12, so 46'-3" comes through as 554.9999999999432, and a plain floor
    /// turns a 97" bar into a 96" one — a different part number.
    /// </summary>
    private static double FloorInches(double value) => Math.Floor(value + 1e-6);

    public static IReadOnlyList<RebarGroup> Plan(PieceDimensions d, RebarRules? rules = null)
    {
        var r = rules ?? new RebarRules();
        var groups = new List<RebarGroup>();
        if (d.LengthIn <= 0 || d.HeightIn <= 0) return groups;

        const int wythes = 2; // one layer in each concrete wythe: "(1) TIF & (1) BIF"

        // ---- longitudinal, along the length, spliced when longer than stock ----
        var run = FloorInches(d.LengthIn - 2 * r.EndCoverIn);
        var rowOffsets = RowOffsets(d.HeightIn, r.SideCoverIn, r.LongitudinalRows);
        var longCount = r.LongitudinalRows * wythes;

        if (run <= r.MaxStockIn)
        {
            groups.Add(new RebarGroup
            {
                PartNumber = PartNumber(r.LongitudinalSize, (int)run),
                Size = r.LongitudinalSize,
                LengthIn = (int)run,
                Count = longCount,
                Direction = "longitudinal",
                StartIn = r.EndCoverIn,
                Offsets = rowOffsets
            });
        }
        else
        {
            var first = (int)r.MaxStockIn;
            var second = (int)FloorInches(run - r.MaxStockIn + r.LapIn);
            var firstNo = PartNumber(r.LongitudinalSize, first);
            var secondNo = PartNumber(r.LongitudinalSize, second);

            groups.Add(new RebarGroup
            {
                PartNumber = firstNo,
                Size = r.LongitudinalSize,
                LengthIn = first,
                Count = longCount,
                Direction = "longitudinal",
                StartIn = r.EndCoverIn,
                Offsets = rowOffsets,
                SplicedWith = secondNo
            });
            groups.Add(new RebarGroup
            {
                PartNumber = secondNo,
                Size = r.LongitudinalSize,
                LengthIn = second,
                Count = longCount,
                Direction = "longitudinal",
                StartIn = r.EndCoverIn + first - r.LapIn,
                Offsets = rowOffsets,
                SplicedWith = firstNo
            });
        }

        // ---- transverse, across the height, at the stated spacing ----
        var span = (int)FloorInches(d.HeightIn - 2 * r.SideCoverIn);
        var positions = new List<double>();
        for (var x = r.EndCoverIn; x <= d.LengthIn - r.EndCoverIn + 1e-6; x += r.TransverseSpacingIn)
            positions.Add(Math.Round(x, 3));

        groups.Add(new RebarGroup
        {
            PartNumber = PartNumber(r.TransverseSize, span),
            Size = r.TransverseSize,
            LengthIn = span,
            Count = positions.Count * wythes,
            Direction = "transverse",
            StartIn = r.SideCoverIn,
            Offsets = positions
        });

        return groups;
    }

    /// <summary>Rows spread evenly between the side covers: bottom, (middle...), top.</summary>
    private static List<double> RowOffsets(double height, double cover, int rows)
    {
        if (rows <= 1) return new List<double> { Math.Round(height / 2.0, 3) };

        var span = height - 2 * cover;
        return Enumerable.Range(0, rows)
            .Select(i => Math.Round(cover + span * i / (rows - 1), 3))
            .ToList();
    }
}
