using Autodesk.Revit.DB;
using GmpPlatform.Core.Tickets;

namespace GmpPlatform.Revit.ShopTickets;

/// <summary>
/// The 2D drawing frame of a precast panel, derived from its own geometry.
/// <para>
/// U runs along the panel length from the START PANEL end (the origin the stub dimensions
/// measure from), V up the panel height, and N is the face normal / thickness direction.
/// Everything the ticket draws is expressed in this frame, in inches, so the AutoCAD side
/// never has to know how the panel happened to be placed in the building.
/// </para>
/// </summary>
internal sealed class PanelFrame
{
    private const double FeetToInches = 12.0;

    private PanelFrame(Face face, XYZ origin, XYZ u, XYZ v, XYZ n, double thicknessFt)
    {
        Face = face;
        Origin = origin;
        U = u;
        V = v;
        N = n;
        ThicknessFt = thicknessFt;
    }

    public Face Face { get; }
    public XYZ Origin { get; }
    public XYZ U { get; }
    public XYZ V { get; }
    public XYZ N { get; }
    public double ThicknessFt { get; }
    public double OppositeFaceAreaSqFt { get; private set; }
    public List<List<XYZ>> Loops { get; } = new();

    /// <summary>
    /// Picks the drawing face: the largest planar face, whose opposite twin gives the
    /// panel thickness. Loops are tessellated into world points for projection.
    /// </summary>
    public static PanelFrame? FromSolid(Solid solid, List<string> warnings)
    {
        var planar = solid.Faces.Cast<Face>().OfType<PlanarFace>().ToList();
        if (planar.Count == 0) return null;

        var face = planar.OrderByDescending(f => f.Area).First();
        var normal = face.FaceNormal.Normalize();

        // The twin is the largest planar face pointing the other way.
        var opposite = planar
            .Where(f => f.FaceNormal.Normalize().DotProduct(normal) < -0.99)
            .OrderByDescending(f => f.Area)
            .FirstOrDefault();

        if (opposite == null)
            warnings.Add("No opposed panel face found; thickness was taken from parameters only.");

        var thickness = opposite == null
            ? 0
            : Math.Abs((opposite.Origin - face.Origin).DotProduct(normal));

        // Tessellate the loops in world coordinates.
        var loops = new List<List<XYZ>>();
        foreach (CurveLoop loop in face.GetEdgesAsCurveLoops())
        {
            var points = new List<XYZ>();
            foreach (var curve in loop)
            {
                // Tessellate() gives straight segments for arcs too, which is what a
                // 2D outline needs; drop the duplicated shared end point.
                var tess = curve.Tessellate();
                for (var i = 0; i < tess.Count - 1; i++) points.Add(tess[i]);
            }

            if (points.Count >= 3) loops.Add(points);
        }

        if (loops.Count == 0) return null;

        // In-plane axes: U along the longest extent of the outer loop, V perpendicular.
        var outer = loops.OrderByDescending(l => l.Count).First();
        var (u, origin) = LongestAxis(outer, normal);
        var v = normal.CrossProduct(u).Normalize();

        // Keep V pointing towards the bulk of the panel so heights come out positive.
        var probe = outer.Aggregate(XYZ.Zero, (acc, p) => acc + p) / outer.Count;
        if ((probe - origin).DotProduct(v) < 0) v = v.Negate();

        var frame = new PanelFrame(face, origin, u, v, normal, thickness)
        {
            OppositeFaceAreaSqFt = opposite?.Area ?? 0
        };
        frame.Loops.AddRange(loops);
        return frame;
    }

    /// <summary>Longest edge direction of the loop, and the loop corner to measure from.</summary>
    private static (XYZ Axis, XYZ Origin) LongestAxis(IReadOnlyList<XYZ> loop, XYZ normal)
    {
        XYZ best = XYZ.BasisX;
        double bestLength = 0;

        for (var i = 0; i < loop.Count; i++)
        {
            var a = loop[i];
            var b = loop[(i + 1) % loop.Count];
            var d = b - a;
            var length = d.GetLength();
            if (length > bestLength) { bestLength = length; best = d; }
        }

        var axis = best.Normalize();

        // Project every point on the axis and start from the lowest — the panel end.
        var reference = loop[0];
        var min = loop.Min(p => (p - reference).DotProduct(axis));
        var minPoint = loop.First(p => Math.Abs((p - reference).DotProduct(axis) - min) < 1e-6);

        // Shift the origin to the lowest corner across the perpendicular direction too.
        var perpendicular = normal.CrossProduct(axis).Normalize();
        var minPerp = loop.Min(p => (p - minPoint).DotProduct(perpendicular));
        var origin = minPoint + perpendicular * minPerp;

        return (axis, origin);
    }

    /// <summary>World point to panel-local inches.</summary>
    public Point2 ToLocalInches(XYZ world)
    {
        var d = world - Origin;
        return new Point2(
            Math.Round(d.DotProduct(U) * FeetToInches, 4),
            Math.Round(d.DotProduct(V) * FeetToInches, 4));
    }

    /// <summary>
    /// Which face a placed item belongs to. Items past the panel mid-plane sit on the far
    /// face. The TIF/BIF convention itself is a client rule we have not had confirmed, so
    /// the naming is recorded as an assumption rather than presented as fact.
    /// </summary>
    public string FaceOf(XYZ world)
    {
        if (ThicknessFt <= 0) return "";

        // Depth runs from 0 at the reference face to -thickness at its twin, so the
        // mid-plane sits at -thickness/2. Items nearer the reference face are on it.
        var depth = (world - Origin).DotProduct(N);
        return depth > ThicknessFt * -0.5 ? ReferenceFaceName : OppositeFaceName;
    }

    /// <summary>Signed distance from the reference face along the thickness direction.</summary>
    public double DepthInches(XYZ world) =>
        Math.Round((world - Origin).DotProduct(N) * FeetToInches, 3);

    /// <summary>
    /// Which client face name the reference (largest) face carries.
    /// <para>
    /// Measured against the approved ticket rather than assumed. Two independent checks
    /// put TIF on the reference face: INS087 sits 0.50" into it and the ticket annotates
    /// those items "℄ (1) INS087 (TIF) ½&quot; RECESS"; PAS032 sits 7.13" in — against the
    /// far face of a 9" panel — and the ticket annotates it "℄ (2) PAS032 (BIF)".
    /// </para>
    /// <para>
    /// An earlier reading took the takeoff note "3&quot; concrete BIF = 533.30 SQFT" as
    /// naming the largest face, but that is a wythe concrete area, which is the same for
    /// either face and so does not identify one. Still to be confirmed by GMP; the depth
    /// travels with every instance so the call can be rechecked.
    /// </para>
    /// </summary>
    public string ReferenceFaceName { get; set; } = "TIF";

    public string OppositeFaceName => ReferenceFaceName == "BIF" ? "TIF" : "BIF";
}
