using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using GmpPlatform.Core.Models;

namespace GmpPlatform.Revit.ShopTickets;

internal static class WallPanelReader
{
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

    public static WallPanelSnapshot Read(Element element)
    {
        var type = element.Document.GetElement(element.GetTypeId()) as ElementType;
        var bbox = element.get_BoundingBox(null);
        var size = bbox == null
            ? (0d, 0d, 0d)
            : (
                Math.Abs(bbox.Max.X - bbox.Min.X) / 12.0, // Revit internal feet already; bbox is in feet
                Math.Abs(bbox.Max.Z - bbox.Min.Z),
                Math.Abs(bbox.Max.Y - bbox.Min.Y));

        // Bounding box values are already in feet in the Revit API.
        if (bbox != null)
        {
            size = (
                Math.Abs(bbox.Max.X - bbox.Min.X),
                Math.Abs(bbox.Max.Z - bbox.Min.Z),
                Math.Abs(bbox.Max.Y - bbox.Min.Y));
        }

        var mark = GetParam(element, BuiltInParameter.ALL_MODEL_MARK)
                   ?? GetParam(element, "Mark")
                   ?? "";

        var design = GetParam(element, "Design No")
                     ?? GetParam(type, "Design No")
                     ?? type?.Name
                     ?? "";

        return new WallPanelSnapshot
        {
            ElementId = element.Id.Value,
            UniqueId = element.UniqueId,
            CategoryName = element.Category?.Name ?? "",
            FamilyName = type is FamilySymbol fs
                ? fs.FamilyName
                : (type?.get_Parameter(BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM)?.AsString() ?? ""),
            TypeName = type?.Name ?? "",
            Mark = mark,
            DesignNo = design,
            LengthFt = Round3(size.Item1),
            HeightFt = Round3(size.Item2),
            ThicknessFt = Round3(size.Item3),
            DocumentTitle = element.Document.Title,
            NeighborMarks = FindNeighborMarks(element),
            Parts = FindNestedParts(element)
        };
    }

    private static IReadOnlyList<string> FindNeighborMarks(Element element)
    {
        var bbox = element.get_BoundingBox(null);
        if (bbox == null) return Array.Empty<string>();

        var doc = element.Document;
        var expanded = new BoundingBoxXYZ
        {
            Min = new XYZ(bbox.Min.X - 2.0, bbox.Min.Y - 2.0, bbox.Min.Z - 2.0),
            Max = new XYZ(bbox.Max.X + 2.0, bbox.Max.Y + 2.0, bbox.Max.Z + 2.0)
        };

        var outline = new Outline(expanded.Min, expanded.Max);
        var filter = new BoundingBoxIntersectsFilter(outline);
        var marks = new FilteredElementCollector(doc)
            .WherePasses(filter)
            .WhereElementIsNotElementType()
            .Where(e => e.Id != element.Id)
            .Where(e =>
            {
                var c = (int)(e.Category?.Id.Value ?? 0);
                return c == (int)BuiltInCategory.OST_StructuralFraming ||
                       c == (int)BuiltInCategory.OST_Walls;
            })
            .Select(e => GetParam(e, BuiltInParameter.ALL_MODEL_MARK) ?? e.Name)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        return marks;
    }

    private static IReadOnlyList<PartQuantity> FindNestedParts(Element element)
    {
        if (element is not FamilyInstance fi)
            return Array.Empty<PartQuantity>();

        var parts = new List<PartQuantity>();
        try
        {
            var subIds = fi.GetSubComponentIds();
            foreach (var id in subIds)
            {
                var sub = element.Document.GetElement(id);
                if (sub == null) continue;
                var type = element.Document.GetElement(sub.GetTypeId()) as ElementType;
                var partNo = GetParam(sub, "Part Number")
                             ?? GetParam(type, "Part Number")
                             ?? GetParam(sub, BuiltInParameter.ALL_MODEL_MARK)
                             ?? type?.Name
                             ?? sub.Name;
                if (string.IsNullOrWhiteSpace(partNo)) continue;

                var existing = parts.FirstOrDefault(p =>
                    string.Equals(p.PartNumber, partNo, StringComparison.OrdinalIgnoreCase));
                if (existing != null) existing.Quantity += 1;
                else parts.Add(new PartQuantity { PartNumber = partNo, Quantity = 1, Source = "nested" });
            }
        }
        catch
        {
            // Some instances do not expose subcomponents; demo continues with placeholders.
        }

        return parts;
    }

    private static string? GetParam(Element? element, BuiltInParameter bip)
    {
        if (element == null) return null;
        var p = element.get_Parameter(bip);
        var v = p?.AsString();
        if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        v = p?.AsValueString();
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }

    private static string? GetParam(Element? element, string name)
    {
        if (element == null) return null;
        var p = element.LookupParameter(name);
        var v = p?.AsString();
        if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        v = p?.AsValueString();
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }

    private static double Round3(double value) => Math.Round(value, 3);
}
