using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using GmpPlatform.Core.Models;

namespace GmpPlatform.Revit.ShopTickets;

internal static class WallPanelReader
{
    /// <summary>Tindall part numbers look like INS087, PAS018, RSX03143, SPS025.</summary>
    private static readonly Regex PartNumberPattern = new(@"\b[A-Z]{2,3}[A-Z]?\d{3,5}\b", RegexOptions.Compiled);

    public static bool TryGetSelectedPanel(UIDocument uidoc, out Element element, out string error)
    {
        element = null!;
        error = "";

        var doc = uidoc.Document;
        var ids = uidoc.Selection.GetElementIds();
        if (ids.Count != 1)
        {
            error = "Select exactly one wall panel (Structural Framing or Wall), then run the command again.";
            return false;
        }

        element = doc.GetElement(ids.First());
        if (element == null)
        {
            error = "Could not resolve the selected element.";
            return false;
        }

        var catId = (int)(element.Category?.Id.Value ?? 0);
        var ok =
            catId == (int)BuiltInCategory.OST_StructuralFraming ||
            catId == (int)BuiltInCategory.OST_Walls ||
            element.Category?.Name.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) >= 0;

        if (!ok)
        {
            error =
                $"Selected category '{element.Category?.Name}' is not supported in this demo. " +
                "Select Structural Framing (wall panel) or a Wall.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Everything read from the model in one pass, including the raw contained elements.
    /// The snapshot is the flattened view of this; the ticket exporter needs the elements
    /// themselves so it can place and dimension each part.
    /// </summary>
    internal sealed class WallPanelSnapshotContext
    {
        public string Mark { get; set; } = "";
        public string Pid { get; set; } = "";
        public string DesignNo { get; set; } = "";
        public string FamilyName { get; set; } = "";
        public string TypeName { get; set; } = "";
        public double LengthFt { get; set; }
        public double HeightFt { get; set; }
        public double ThicknessFt { get; set; }
        public string DimensionSource { get; set; } = "";
        public List<string> Warnings { get; } = new();
        public List<Element> ContainedElements { get; } = new();
    }

    /// <summary>Part numbers embedded in a family or connection-assembly type name.</summary>
    internal static List<string> ExtractPartNumbers(string typeName) =>
        PartNumberPattern.Matches(typeName ?? "")
            .Cast<Match>()
            .Select(m => m.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static WallPanelSnapshot Read(Element element)
    {
        var ctx = ReadContext(element);
        return new WallPanelSnapshot
        {
            ElementId = element.Id.Value,
            UniqueId = element.UniqueId,
            CategoryName = element.Category?.Name ?? "",
            FamilyName = ctx.FamilyName,
            TypeName = ctx.TypeName,
            Mark = ctx.Mark,
            Pid = ctx.Pid,
            DesignNo = ctx.DesignNo,
            LengthFt = Round3(ctx.LengthFt),
            HeightFt = Round3(ctx.HeightFt),
            ThicknessFt = Round3(ctx.ThicknessFt),
            DimensionSource = ctx.DimensionSource,
            DocumentTitle = element.Document.Title,
            NeighborMarks = FindNeighborMarks(element),
            Parts = SummariseParts(element.Document, ctx),
            DataWarnings = ctx.Warnings
        };
    }

    internal static WallPanelSnapshotContext ReadContext(Element element)
    {
        var type = element.Document.GetElement(element.GetTypeId()) as ElementType;
        var ctx = new WallPanelSnapshotContext
        {
            FamilyName = type is FamilySymbol fs
                ? fs.FamilyName
                : (type?.get_Parameter(BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM)?.AsString() ?? ""),
            TypeName = type?.Name ?? ""
        };
        var warnings = ctx.Warnings;

        // ---- identity -------------------------------------------------------
        // CONTROL_MARK is the piece mark the ticket prints (AFIP-1035). The built-in
        // Mark parameter holds a category label ("PC WALL PANEL") on this content and
        // is identical across every panel, so it is only a last resort.
        var mark = Param(element, "CONTROL_MARK")
                   ?? Param(element, BuiltInParameter.ALL_MODEL_MARK)
                   ?? "";

        if (Param(element, "CONTROL_MARK") == null)
            warnings.Add("CONTROL_MARK not found on the panel; falling back to the built-in Mark parameter.");

        var pid = Param(element, "CONTROL_NUMBER") ?? "";

        var design = Param(element, "DESIGN_NUMBER")
                     ?? Param(type, "DESIGN_NUMBER")
                     ?? Param(element, "Design No")
                     ?? type?.Name
                     ?? "";

        // ---- dimensions -----------------------------------------------------
        // The model carries the exact printed values. DIM_LENGTH is the long direction
        // (46'-3") and DIM_WIDTH is the panel height as drawn (12'-1 3/4"); the bounding
        // box disagrees with both (it reports 46'-4") and its axes depend on how the
        // panel happens to be placed, so parameters are strongly preferred.
        var lengthFt = Length(element, type, "DIM_LENGTH");
        var heightFt = Length(element, type, "DIM_WIDTH");
        var thicknessFt = Length(element, type, "DIM_THICKNESS");
        var dimensionSource = "parameters (DIM_LENGTH / DIM_WIDTH / DIM_THICKNESS)";

        if (lengthFt <= 0 || heightFt <= 0 || thicknessFt <= 0)
        {
            // Fall back to the bounding box, sorting extents by size rather than assuming
            // which world axis is which - these panels are modeled standing up.
            var bbox = element.get_BoundingBox(null);
            if (bbox != null)
            {
                var extents = new[]
                {
                    Math.Abs(bbox.Max.X - bbox.Min.X),
                    Math.Abs(bbox.Max.Y - bbox.Min.Y),
                    Math.Abs(bbox.Max.Z - bbox.Min.Z)
                }.OrderByDescending(v => v).ToArray();

                if (lengthFt <= 0) lengthFt = extents[0];
                if (heightFt <= 0) heightFt = extents[1];
                if (thicknessFt <= 0) thicknessFt = extents[2];
            }

            dimensionSource = "bounding box (approximate)";
            warnings.Add(
                "DIM_LENGTH / DIM_WIDTH / DIM_THICKNESS were not all available; " +
                "dimensions came from the bounding box and may differ from the printed values.");
        }

        // ---- parts ----------------------------------------------------------
        ctx.ContainedElements.AddRange(FindContainedElements(element, warnings));

        ctx.Mark = mark;
        ctx.Pid = pid;
        ctx.DesignNo = design;
        ctx.LengthFt = lengthFt;
        ctx.HeightFt = heightFt;
        ctx.ThicknessFt = thicknessFt;
        ctx.DimensionSource = dimensionSource;
        return ctx;
    }

    /// <summary>
    /// Parts genuinely inside the panel, by geometric containment. Embeds are placed as
    /// independent Specialty Equipment instances (not sub-components), and a bounding-box
    /// test pulls in hardware from the neighbouring panel, so neither shortcut works here.
    /// </summary>
    private static List<Element> FindContainedElements(Element element, List<string> warnings)
    {
        var doc = element.Document;

        try
        {
            var bbox = element.get_BoundingBox(null);
            var collector = new FilteredElementCollector(doc).WhereElementIsNotElementType();

            // Cheap bounding-box narrowing first; the solid intersection test is the one
            // that decides, but it is far more expensive to run against the whole model.
            if (bbox != null)
                collector = collector.WherePasses(new BoundingBoxIntersectsFilter(new Outline(bbox.Min, bbox.Max)));

            var contained = collector
                .WherePasses(new ElementIntersectsElementFilter(element))
                .ToElements()
                .Where(e => e.Id != element.Id)
                .ToList();

            if (contained.Count == 0)
                warnings.Add("No parts are geometrically contained in this panel; the bill of material will be empty.");

            // Name what was skipped, grouped by type. "39 elements were skipped" reads like
            // lost data and sent us looking for missing BOM rows; the census showed the
            // skipped items are the ones the approved ticket does not give rows to either —
            // 36 HCA studs that belong to an embed's description, the beadboard that is
            // counted as insulation area, and two empty container families. A drafter can
            // only check that if the warning says which types they were.
            var unmatched = contained
                .Select(e => new
                {
                    Element = e,
                    TypeName = (doc.GetElement(e.GetTypeId()) as ElementType)?.Name ?? e.Name ?? ""
                })
                .Where(x => ExtractPartNumbers(x.TypeName).Count == 0)
                .GroupBy(x => x.TypeName)
                .Select(g => $"{g.Key} x{g.Count()}")
                .OrderBy(s => s)
                .ToList();

            if (unmatched.Count > 0)
                warnings.Add(
                    $"{unmatched.Count} contained type(s) carry no part number in their type name and are " +
                    $"not given their own bill-of-material row: {string.Join(", ", unmatched)}. " +
                    "Confirm each is meant to roll up into a parent item rather than be listed.");

            return contained;
        }
        catch (Exception ex)
        {
            warnings.Add("Part extraction failed: " + ex.Message);
            return new List<Element>();
        }
    }

    /// <summary>Flattens the contained elements into BOM lines for the run report.</summary>
    private static IReadOnlyList<PartQuantity> SummariseParts(Document doc, WallPanelSnapshotContext ctx)
    {
        var parts = new List<PartQuantity>();

        foreach (var item in ctx.ContainedElements)
        {
            var typeName = (doc.GetElement(item.GetTypeId()) as ElementType)?.Name ?? item.Name ?? "";
            var isConnection = item.Category?.Id.Value == (long)BuiltInCategory.OST_StructConnections;

            // Connection assemblies bundle several parts in their type name,
            // e.g. "X-27 : PAS018 + PAS089 + EMS001".
            foreach (var number in ExtractPartNumbers(typeName))
                Add(parts, number, isConnection ? "connection assembly" : "contained");
        }

        return parts
            .OrderBy(p => p.PartNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void Add(ICollection<PartQuantity> parts, string partNumber, string source)
    {
        var existing = parts.FirstOrDefault(p =>
            string.Equals(p.PartNumber, partNumber, StringComparison.OrdinalIgnoreCase));

        if (existing != null) existing.Quantity += 1;
        else parts.Add(new PartQuantity { PartNumber = partNumber, Quantity = 1, Source = source });
    }

    private static IReadOnlyList<string> FindNeighborMarks(Element element)
    {
        var bbox = element.get_BoundingBox(null);
        if (bbox == null) return Array.Empty<string>();

        var doc = element.Document;
        var outline = new Outline(
            new XYZ(bbox.Min.X - 2.0, bbox.Min.Y - 2.0, bbox.Min.Z - 2.0),
            new XYZ(bbox.Max.X + 2.0, bbox.Max.Y + 2.0, bbox.Max.Z + 2.0));

        return new FilteredElementCollector(doc)
            .WherePasses(new BoundingBoxIntersectsFilter(outline))
            .WhereElementIsNotElementType()
            .Where(e => e.Id != element.Id)
            .Where(e =>
            {
                var c = (int)(e.Category?.Id.Value ?? 0);
                return c == (int)BuiltInCategory.OST_StructuralFraming ||
                       c == (int)BuiltInCategory.OST_Walls;
            })
            .Select(e => Param(e, "CONTROL_MARK") ?? Param(e, BuiltInParameter.ALL_MODEL_MARK) ?? e.Name)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList()!;
    }

    /// <summary>Length parameter in feet, instance first then type; 0 when absent.</summary>
    private static double Length(Element element, Element? type, string name)
    {
        foreach (var host in new[] { element, type })
        {
            var p = host?.LookupParameter(name);
            if (p is { StorageType: StorageType.Double } && p.HasValue)
            {
                var v = p.AsDouble();
                if (v > 0) return v;
            }
        }

        return 0;
    }

    private static string? Param(Element? element, BuiltInParameter bip)
    {
        if (element == null) return null;
        var p = element.get_Parameter(bip);
        var v = p?.AsString();
        if (!string.IsNullOrWhiteSpace(v)) return v!.Trim();
        v = p?.AsValueString();
        return string.IsNullOrWhiteSpace(v) ? null : v!.Trim();
    }

    private static string? Param(Element? element, string name)
    {
        if (element == null) return null;
        var p = element.LookupParameter(name);
        var v = p?.AsString();
        if (!string.IsNullOrWhiteSpace(v)) return v!.Trim();
        v = p?.AsValueString();
        return string.IsNullOrWhiteSpace(v) ? null : v!.Trim();
    }

    private static double Round3(double value) => Math.Round(value, 3);
}
