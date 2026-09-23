using Autodesk.Revit.DB;
using GmpPlatform.Core.Tickets;
using WallPanelSnapshotContext = GmpPlatform.Revit.ShopTickets.WallPanelReader.WallPanelSnapshotContext;

namespace GmpPlatform.Revit.ShopTickets;

/// <summary>
/// Builds the <see cref="PieceTicket"/> hand-off from a precast panel.
/// <para>
/// The panel frame is derived from geometry rather than from world axes: these panels are
/// modeled standing up, and nothing guarantees which world axis is which. The two largest
/// opposed planar faces are the panel faces; their shared normal is the thickness direction,
/// the longest in-plane extent is the panel length, and the remaining one is its height.
/// </para>
/// </summary>
internal static class PieceTicketExporter
{
    private const double FeetToInches = 12.0;

    public static PieceTicket Build(Element panel, WallPanelSnapshotContext ctx)
    {
        var doc = panel.Document;
        var type = doc.GetElement(panel.GetTypeId()) as ElementType;
        var warnings = new List<string>(ctx.Warnings);

        var ticket = new PieceTicket
        {
            GeneratedUtc = DateTime.UtcNow.ToString("u"),
            SourceModel = doc.Title,
            Identity = BuildIdentity(doc, panel, type, ctx),
            Dimensions = BuildDimensions(panel, type, ctx)
        };

        var solid = LargestSolid(panel);
        if (solid == null)
        {
            warnings.Add("No solid geometry found on the panel; outline and quantities are unavailable.");
            ticket.Warnings = warnings;
            return ticket;
        }

        var frame = PanelFrame.FromSolid(solid, warnings);
        if (frame == null)
        {
            warnings.Add("Could not establish a panel face frame; outline and part positions are unavailable.");
            ticket.Warnings = warnings;
            return ticket;
        }

        ticket.Geometry = BuildGeometry(frame, warnings);
        ticket.Geometry.AngledFaces = FindAngledFaces(solid, warnings);
        ticket.Parts = BuildParts(doc, panel, frame, ctx, warnings);
        ticket.Quantities = BuildQuantities(panel, type, frame, ticket.Dimensions, warnings);
        ticket.Warnings = warnings;
        return ticket;
    }

    // ------------------------------------------------------------------ identity

    private static PieceIdentity BuildIdentity(Document doc, Element panel, ElementType? type, WallPanelSnapshotContext ctx)
    {
        var info = doc.ProjectInformation;
        return new PieceIdentity
        {
            Mark = ctx.Mark,
            Pid = ctx.Pid,
            DesignNo = ctx.DesignNo,
            Code = Str(panel, "CODE") ?? Str(type, "CODE") ?? "",
            JobNumber = info?.Number ?? "",
            ProjectName = info?.Name ?? "",
            Plant = Str(info, "PROJECT_CLIENT_PRECAST_MANUFACTURER") ?? "",
            DrawnBy = Str(panel, "TICKET_DETAILED_USER_CURRENT") ?? Str(panel, "SHT_DRAWN_BY") ?? "",
            CheckedBy = Str(panel, "SHT_CHECKED_BY") ?? "",
            ProjectLead = Str(info, "CEG Project Manager") ?? "",
            EngineerChecked = Str(panel, "SHT_ENGR_CHECKED") ?? "",
            ReleasedForJobUse = Str(panel, "TICKET_RELEASED_DATE_CURRENT") ?? "",
            Revision = Str(panel, "SHT_REVISION") ?? "",
            RequiredCount = 1,
            FamilyName = ctx.FamilyName,
            TypeName = ctx.TypeName,
            ElementId = panel.Id.Value
        };
    }

    /// <summary>
    /// Feet to inches, rounded to a thousandth. Revit stores feet, so 46'-3" multiplies out to
    /// 554.9999999999432; left unrounded that noise crosses the contract and changed a rebar
    /// part number downstream. Clean numbers at the boundary, tolerant arithmetic behind it.
    /// </summary>
    private static double Inches(double feet) => TicketUnits.Inches(feet);

    private static PieceDimensions BuildDimensions(Element panel, ElementType? type, WallPanelSnapshotContext ctx) =>
        new()
        {
            LengthIn = Inches(ctx.LengthFt),
            HeightIn = Inches(ctx.HeightFt),
            ThicknessIn = Inches(ctx.ThicknessFt),
            WytheInnerIn = Inches(Len(panel, type, "DIM_WYTHE_INNER")),
            WytheInsulationIn = Inches(Len(panel, type, "DIM_WYTHE_INSULATION")),
            WytheOuterIn = Inches(Len(panel, type, "DIM_WYTHE_OUTER")),
            InsulationLengthIn = Inches(Len(panel, type, "dimension_insulation_length")),
            InsulationHeightIn = Inches(Len(panel, type, "dimension_insulation_width")),
            SolidZoneTopIn = Inches(Len(panel, type, "Solid_Zone_Top")),
            SolidZoneBottomIn = Inches(Len(panel, type, "Solid_Zone_Bottom")),
            SolidZoneEnd1In = Inches(Len(panel, type, "Solid_Zone_End_1")),
            SolidZoneEnd2In = Inches(Len(panel, type, "Solid_Zone_End_2")),
            Source = ctx.DimensionSource
        };

    // ------------------------------------------------------------------ geometry

    private static PieceGeometry BuildGeometry(PanelFrame frame, List<string> warnings)
    {
        var geometry = new PieceGeometry
        {
            FaceAreaSqFt = Math.Round(frame.Face.Area, 4),
            FaceLoopCount = frame.Loops.Count
        };

        if (frame.Loops.Count == 0)
        {
            warnings.Add("The panel face returned no edge loops; no outline could be exported.");
            return geometry;
        }

        // The longest loop is the outer boundary; the rest are openings cut through the face.
        var ordered = frame.Loops
            .Select(loop => loop.Select(frame.ToLocalInches).ToList())
            .OrderByDescending(Perimeter)
            .ToList();

        geometry.Outline = ordered[0];
        geometry.Openings = ordered.Skip(1).ToList();
        return geometry;
    }

    /// <summary>
    /// 45-degree faces, read from the solid. What they represent is not assumed: on AFIP-1035
    /// they turn out to be the corbel slope, not the ½" edge chamfers the ticket calls out,
    /// which are not modelled. The measurement is reported; the meaning is GMP's to confirm.
    /// </summary>
    private static List<AngledFace> FindAngledFaces(Solid solid, List<string> warnings)
    {
        var faces = new List<AngledFace>();

        try
        {
            foreach (Face face in solid.Faces)
            {
                if (face is not PlanarFace planar) continue;

                // A 45-degree face has two direction cosines near 0.707 and one near zero.
                var n = planar.FaceNormal;
                var components = new[] { Math.Abs(n.X), Math.Abs(n.Y), Math.Abs(n.Z) }.OrderBy(v => v).ToArray();
                var isFortyFive = components[0] < 0.01 &&
                                  Math.Abs(components[1] - 0.7071) < 0.01 &&
                                  Math.Abs(components[2] - 0.7071) < 0.01;
                if (!isFortyFive) continue;

                // The face is length x slantWidth; the leg of the bevel is slantWidth / sqrt(2).
                var loop = planar.GetEdgesAsCurveLoops().FirstOrDefault();
                if (loop == null) continue;

                var longest = loop.Max(c => c.ApproximateLength);
                if (longest <= 1e-6) continue;

                var slant = planar.Area / longest;
                faces.Add(new AngledFace
                {
                    LegIn = Math.Round(slant / Math.Sqrt(2.0) * FeetToInches, 3),
                    LengthIn = Math.Round(longest * FeetToInches, 2),
                    AreaSqIn = Math.Round(planar.Area * 144.0, 2)
                });
            }

            if (faces.Count > 0)
            {
                var sizes = faces
                    .Select(f => $"{f.LegIn:0.###}\" leg x {f.LengthIn:0.###}\" long")
                    .Distinct()
                    .OrderBy(s => s);
                warnings.Add($"{faces.Count} angled (45-degree) face(s) measured: " +
                             string.Join(", ", sizes) +
                             " - confirm what these represent; the 1/2\" edge chamfers the ticket " +
                             "calls out are not modelled, so they are not drawn from geometry.");
            }
        }
        catch (Exception ex)
        {
            warnings.Add("Angled-face detection failed: " + ex.Message);
        }

        return faces;
    }

    private static double Perimeter(IReadOnlyList<Point2> loop)
    {
        double total = 0;
        for (var i = 0; i < loop.Count; i++)
        {
            var a = loop[i];
            var b = loop[(i + 1) % loop.Count];
            total += Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        }

        return total;
    }

    // ------------------------------------------------------------------ parts

    private static List<TicketPart> BuildParts(
        Document doc, Element panel, PanelFrame frame, WallPanelSnapshotContext ctx, List<string> warnings)
    {
        var parts = new List<TicketPart>();

        foreach (var contained in ctx.ContainedElements)
        {
            var typeName = (doc.GetElement(contained.GetTypeId()) as ElementType)?.Name ?? contained.Name ?? "";
            var numbers = WallPanelReader.ExtractPartNumbers(typeName);
            if (numbers.Count == 0) continue;

            var isConnection = contained.Category?.Id.Value == (long)BuiltInCategory.OST_StructConnections;
            var point = LocationOf(contained);

            foreach (var number in numbers)
            {
                var part = parts.FirstOrDefault(p =>
                    string.Equals(p.PartNumber, number, StringComparison.OrdinalIgnoreCase));

                if (part == null)
                {
                    part = new TicketPart
                    {
                        PartNumber = number,
                        Source = isConnection ? "connection assembly" : "contained"
                    };

                    var (description, source) = ReadDescription(doc, contained);
                    part.Description = description;
                    part.DescriptionSource = source;

                    parts.Add(part);
                }

                part.Quantity += 1;

                if (point == null) continue;
                var local = frame.ToLocalInches(point);
                part.Instances.Add(new PartInstance
                {
                    X = local.X,
                    Y = local.Y,
                    Face = frame.FaceOf(point),
                    DepthIn = frame.DepthInches(point),
                    ElementId = contained.Id.Value
                });
            }
        }

        var missingPositions = parts.Count(p => p.Instances.Count == 0);
        if (missingPositions > 0)
            warnings.Add($"{missingPositions} part type(s) had no usable insertion point; they cannot be dimensioned.");

        return parts.OrderBy(p => p.PartNumber, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// The item's own description, for the second line of its callout. Tried in order of how
    /// specific each parameter is, and the winning parameter name is recorded alongside the
    /// text so the drawing's wording can be traced to its source rather than trusted.
    /// <para>
    /// Nothing is invented here: the approved ticket's <c>½" RECESS</c> is the drafter's
    /// phrasing, and whether the model's text matches it is a question for GMP, not something
    /// to paper over by reformatting whatever we find.
    /// </para>
    /// </summary>
    private static (string Text, string Source) ReadDescription(Document doc, Element element)
    {
        var type = doc.GetElement(element.GetTypeId()) as ElementType;

        // IDENTITY_DESCRIPTION first. IDENTITY_DESCRIPTION_SHORT sounds like the better fit
        // for a drawing but on this model it simply repeats the part number — "PAS018" for
        // PAS018 — so putting it first produced descriptions that said nothing.
        string[] candidates =
        {
            "IDENTITY_DESCRIPTION",
            "DESCRIPTION",
            "IDENTITY_DESCRIPTION_SHORT"
        };

        foreach (var name in candidates)
        {
            var onInstance = Str(element, name);
            if (!string.IsNullOrWhiteSpace(onInstance)) return (onInstance!.Trim(), "instance." + name);

            var onType = Str(type, name);
            if (!string.IsNullOrWhiteSpace(onType)) return (onType!.Trim(), "type." + name);
        }

        var builtIn = type?.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION)?.AsString();
        return string.IsNullOrWhiteSpace(builtIn)
            ? ("", "")
            : (builtIn!.Trim(), "type.Description");
    }

    private static XYZ? LocationOf(Element element)
    {
        switch (element.Location)
        {
            case LocationPoint lp:
                return lp.Point;
            case LocationCurve lc:
                return (lc.Curve.GetEndPoint(0) + lc.Curve.GetEndPoint(1)) * 0.5;
            default:
                var bb = element.get_BoundingBox(null);
                return bb == null ? null : (bb.Min + bb.Max) * 0.5;
        }
    }

    // ------------------------------------------------------------------ quantities

    private static PieceQuantities BuildQuantities(
        Element panel, ElementType? type, PanelFrame frame, PieceDimensions dims, List<string> warnings)
    {
        var density = Num(panel, type, "WEIGHT_PER_UNIT");
        if (density <= 0) density = 150.0;

        var faceSqFt = frame.Face.Area;
        var innerFt = dims.WytheInnerIn / FeetToInches;
        var outerFt = dims.WytheOuterIn / FeetToInches;

        var q = new PieceQuantities
        {
            BottomFaceAreaSqFt = Math.Round(faceSqFt, 4),
            TopFaceAreaSqFt = Math.Round(frame.OppositeFaceAreaSqFt, 4),
            DensityPcf = density
        };

        if (innerFt > 0 && outerFt > 0)
        {
            q.BottomConcreteCuYd = Math.Round(faceSqFt * outerFt / 27.0, 2);
            q.TopConcreteCuYd = Math.Round(frame.OppositeFaceAreaSqFt * innerFt / 27.0, 2);
            // The ticket prints weight rounded to the nearest 100 lb (42,800).
            var weight = (q.BottomConcreteCuYd + q.TopConcreteCuYd) * 27.0 * density;
            q.WeightLb = Math.Round(weight / 100.0) * 100.0;
            q.Derived.Add("concrete volumes from face area x wythe thickness");
            q.Derived.Add("weight from concrete volume x density");
        }
        else
        {
            warnings.Add("Wythe thicknesses unavailable; concrete volumes and weight were not computed.");
        }

        if (dims.InsulationLengthIn > 0 && dims.InsulationHeightIn > 0)
        {
            q.InsulationSqFt = Math.Round(
                dims.InsulationLengthIn * dims.InsulationHeightIn / 144.0, 0);
            q.Derived.Add("insulation area from insulation length x height (deduction rule unconfirmed)");
        }

        return q;
    }

    // ------------------------------------------------------------------ helpers

    private static Solid? LargestSolid(Element element)
    {
        var options = new Options { DetailLevel = ViewDetailLevel.Fine, ComputeReferences = false };
        var solids = new List<Solid>();
        Collect(element.get_Geometry(options), solids, 0);
        return solids.OrderByDescending(s => s.Volume).FirstOrDefault();
    }

    private static void Collect(GeometryElement? geometry, ICollection<Solid> output, int depth)
    {
        if (geometry == null || depth > 3) return;
        foreach (var item in geometry)
        {
            switch (item)
            {
                case Solid s when s.Faces.Size > 0 && s.Volume > 1e-6:
                    output.Add(s);
                    break;
                case GeometryInstance gi:
                    Collect(gi.GetInstanceGeometry(), output, depth + 1);
                    break;
            }
        }
    }

    private static string? Str(Element? element, string name)
    {
        var p = element?.LookupParameter(name);
        var v = p?.AsString();
        if (!string.IsNullOrWhiteSpace(v)) return v!.Trim();
        v = p?.AsValueString();
        return string.IsNullOrWhiteSpace(v) ? null : v!.Trim();
    }

    private static double Len(Element element, Element? type, string name)
    {
        foreach (var host in new[] { element, type })
        {
            var p = host?.LookupParameter(name);
            if (p is { StorageType: StorageType.Double } && p.HasValue && p.AsDouble() > 0)
                return p.AsDouble();
        }

        return 0;
    }

    private static double Num(Element element, Element? type, string name)
    {
        foreach (var host in new[] { element, type })
        {
            var p = host?.LookupParameter(name);
            if (p is { StorageType: StorageType.Double } && p.HasValue) return p.AsDouble();
            if (p is { StorageType: StorageType.Integer } && p.HasValue) return p.AsInteger();
        }

        return 0;
    }
}
