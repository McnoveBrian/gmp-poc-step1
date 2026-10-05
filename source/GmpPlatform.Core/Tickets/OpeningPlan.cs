namespace GmpPlatform.Core.Tickets;

/// <summary>Where an opening sits in the piece.</summary>
public enum OpeningKind
{
    /// <summary>An inner loop of the face: a blockout with concrete all round it.</summary>
    Inner,

    /// <summary>A cut-out from the bottom edge, found in the outline (AFIP-1035's B.O.-1).</summary>
    BottomNotch,

    /// <summary>A cut-out from the top edge, found in the outline.</summary>
    TopNotch
}

/// <summary>One numbered opening, by its extents in panel coordinates (X = station, Y up).</summary>
public sealed class PlannedOpening
{
    public PlannedOpening(int number, OpeningKind kind, double minX, double maxX, double minY, double maxY)
    {
        Number = number;
        Kind = kind;
        MinX = minX;
        MaxX = maxX;
        MinY = minY;
        MaxY = maxY;
    }

    /// <summary>The n in "B.O.-n".</summary>
    public int Number { get; }
    public OpeningKind Kind { get; }
    public double MinX { get; }
    public double MaxX { get; }
    public double MinY { get; }
    public double MaxY { get; }
    public double Width => MaxX - MinX;
    public double Height => MaxY - MinY;
}

/// <summary>
/// Openings that start and stop at the same stations. The approved ticket dimensions such a
/// stack once and names it after all its members: "START B.O.-2,3", "2'-0" (B.O.-2,3)".
/// </summary>
public sealed class OpeningGroup
{
    public OpeningGroup(IReadOnlyList<PlannedOpening> openings, double pieceHeightIn)
    {
        Openings = openings;
        Start = openings.Min(o => o.MinX);
        Stop = openings.Max(o => o.MaxX);
        Below = openings.Average(o => (o.MinY + o.MaxY) / 2.0) <= pieceHeightIn / 2.0;
    }

    public IReadOnlyList<PlannedOpening> Openings { get; }

    /// <summary>Lower station: the end nearer START PANEL.</summary>
    public double Start { get; }

    public double Stop { get; }

    /// <summary>
    /// Annotated off the bottom edge when the group sits in the lower half. On AFIP-1035 both
    /// groups are dimensioned below the piece; the rule for the upper half is by symmetry and
    /// is unconfirmed.
    /// </summary>
    public bool Below { get; }

    /// <summary>"2,3" — the member numbers, as the suffix of a label.</summary>
    public string Numbers => string.Join(",", Openings.Select(o => o.Number));

    /// <summary>
    /// The vertical chain for the dimension column beside the piece: the height split at every
    /// member's top and bottom, each span carrying the opening it measures, if any.
    /// AFIP-1035's BLOCKOUT-1 column reads 3'-11⅞" (B.O.-1) then 8'-1⅞" to the top.
    /// </summary>
    public IReadOnlyList<VerticalSpan> VerticalSpans(double pieceHeightIn)
    {
        var levels = new List<double> { 0, pieceHeightIn };
        foreach (var o in Openings)
        {
            levels.Add(o.MinY);
            levels.Add(o.MaxY);
        }

        var sorted = levels
            .Select(y => Math.Max(0, Math.Min(pieceHeightIn, y)))
            .OrderBy(y => y)
            .Aggregate(new List<double>(), (acc, y) =>
            {
                if (acc.Count == 0 || y - acc[acc.Count - 1] > OpeningPlan.Tolerance) acc.Add(y);
                return acc;
            });

        var spans = new List<VerticalSpan>();
        for (var i = 0; i + 1 < sorted.Count; i++)
        {
            var from = sorted[i];
            var to = sorted[i + 1];
            var opening = Openings.FirstOrDefault(o =>
                Math.Abs(o.MinY - from) <= OpeningPlan.Tolerance && Math.Abs(o.MaxY - to) <= OpeningPlan.Tolerance);
            spans.Add(new VerticalSpan(from, to, opening?.Number));
        }

        return spans;
    }
}

/// <summary>One span of a vertical dimension chain; <see cref="Opening"/> is set when it measures one.</summary>
public sealed class VerticalSpan
{
    public VerticalSpan(double from, double to, int? opening)
    {
        From = from;
        To = to;
        Opening = opening;
    }

    public double From { get; }
    public double To { get; }
    public int? Opening { get; }
}

/// <summary>
/// Finds and numbers a piece's openings: the face's inner loops, plus the cut-outs from its
/// top and bottom edges, which the exporter delivers as part of the outline rather than as
/// loops — AFIP-1035's B.O.-1 is one. Cut-outs at the two ends are not openings: on FIP-1171
/// they are the 8" end pockets, which belong to the edge details.
/// </summary>
public static class OpeningPlan
{
    /// <summary>Coordinates closer than this are the same edge or station (1/16").</summary>
    public const double Tolerance = 1.0 / 16.0;

    /// <summary>Loops smaller than this (sq in) are modelling noise, not openings.</summary>
    public const double MinimumArea = 1.0;

    /// <summary>
    /// Numbered from the START PANEL end (ascending station), top to bottom where openings
    /// share a station. AFIP-1035's B.O.-1 is the opening nearest START PANEL; the order
    /// within a stack (B.O.-2 above B.O.-3) is unconfirmed.
    /// </summary>
    public static IReadOnlyList<PlannedOpening> Find(PieceGeometry geometry)
    {
        var found = new List<(OpeningKind Kind, double MinX, double MaxX, double MinY, double MaxY)>();

        foreach (var loop in geometry.Openings)
        {
            var pts = Distinct(loop);
            if (pts.Count < 3 || Math.Abs(SignedArea(pts)) < MinimumArea) continue;
            found.Add((OpeningKind.Inner, pts.Min(p => p.X), pts.Max(p => p.X), pts.Min(p => p.Y), pts.Max(p => p.Y)));
        }

        found.AddRange(EdgeNotches(Distinct(geometry.Outline)));

        return found
            .OrderBy(o => Math.Round(o.MinX / Tolerance))
            .ThenByDescending(o => o.MaxY)
            .Select((o, i) => new PlannedOpening(i + 1, o.Kind, o.MinX, o.MaxX, o.MinY, o.MaxY))
            .ToList();
    }

    /// <summary>Openings sharing both start and stop stations, in station order.</summary>
    public static IReadOnlyList<OpeningGroup> Group(IReadOnlyList<PlannedOpening> openings, double pieceHeightIn) =>
        openings
            .GroupBy(o => (Math.Round(o.MinX / Tolerance), Math.Round(o.MaxX / Tolerance)))
            .Select(g => new OpeningGroup(g.OrderBy(o => o.Number).ToList(), pieceHeightIn))
            .OrderBy(g => g.Start)
            .ToList();

    private static IEnumerable<(OpeningKind, double, double, double, double)> EdgeNotches(IReadOnlyList<Point2> pts)
    {
        if (pts.Count < 4) yield break;

        var minX = pts.Min(p => p.X);
        var maxX = pts.Max(p => p.X);
        var minY = pts.Min(p => p.Y);
        var maxY = pts.Max(p => p.Y);

        foreach (var (edgeY, kind) in new[] { (minY, OpeningKind.BottomNotch), (maxY, OpeningKind.TopNotch) })
        {
            var onEdge = pts.Select(p => Math.Abs(p.Y - edgeY) <= Tolerance).ToArray();
            var first = Array.IndexOf(onEdge, true);
            if (first < 0) continue;

            // Walk the loop once from an edge vertex. Every run of vertices off the edge,
            // between two vertices on it, is an excursion into the piece. It is a notch only
            // when it leaves and rejoins the edge clear of both ends; otherwise it is the rest
            // of the outline, an end pocket, or a step in an L-shaped piece.
            var n = pts.Count;
            var i = first;
            for (var walked = 0; walked < n;)
            {
                var k = (i + 1) % n;
                var run = new List<Point2>();
                while (!onEdge[k])
                {
                    run.Add(pts[k]);
                    k = (k + 1) % n;
                }

                if (run.Count > 0)
                {
                    var xs = run.Select(p => p.X).Concat(new[] { pts[i].X, pts[k].X }).ToList();
                    var clearOfEnds = xs.All(x => x > minX + Tolerance && x < maxX - Tolerance);
                    if (clearOfEnds)
                    {
                        var depthY = kind == OpeningKind.BottomNotch ? run.Max(p => p.Y) : run.Min(p => p.Y);
                        yield return kind == OpeningKind.BottomNotch
                            ? (kind, xs.Min(), xs.Max(), edgeY, depthY)
                            : (kind, xs.Min(), xs.Max(), depthY, edgeY);
                    }
                }

                walked += run.Count + 1;
                i = k;
            }
        }
    }

    /// <summary>The loop without a repeated closing vertex or consecutive duplicates.</summary>
    private static List<Point2> Distinct(IReadOnlyList<Point2> loop)
    {
        var pts = new List<Point2>();
        foreach (var p in loop)
        {
            if (pts.Count > 0 && Same(pts[pts.Count - 1], p)) continue;
            pts.Add(p);
        }

        if (pts.Count > 1 && Same(pts[0], pts[pts.Count - 1])) pts.RemoveAt(pts.Count - 1);
        return pts;
    }

    private static bool Same(Point2 a, Point2 b) =>
        Math.Abs(a.X - b.X) <= Tolerance && Math.Abs(a.Y - b.Y) <= Tolerance;

    private static double SignedArea(IReadOnlyList<Point2> pts)
    {
        var sum = 0.0;
        for (var i = 0; i < pts.Count; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Count];
            sum += a.X * b.Y - b.X * a.Y;
        }

        return sum / 2.0;
    }
}
