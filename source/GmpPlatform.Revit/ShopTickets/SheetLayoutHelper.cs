using Autodesk.Revit.DB;
using GmpPlatform.Core.Settings;

namespace GmpPlatform.Revit.ShopTickets;

/// <summary>Printable sheet region and view-scale fitting helpers.</summary>
internal static class SheetLayoutHelper
{
    private static readonly int[] StandardScales =
    {
        1, 2, 4, 8, 12, 16, 24, 32, 48, 64, 96, 128, 192, 256, 384, 512
    };

    /// <summary>
    /// Content region on the sheet (feet), excluding the title-block column and margins.
    /// </summary>
    public static BoundingBoxUV GetPrintableArea(ViewSheet sheet, PlatformSettings settings)
    {
        var outline = sheet.Outline;
        var minU = outline.Min.U;
        var maxU = outline.Max.U;
        var minV = outline.Min.V;
        var maxV = outline.Max.V;

        // Prefer measuring the actual title-block instance bbox on the sheet.
        var tbBox = GetTitleBlockBoundingBox(sheet);
        if (tbBox != null)
        {
            // Typical Tindall/client blocks sit on the right; content is everything left of them.
            var tbMinU = tbBox.Min.X;
            var tbMaxU = tbBox.Max.X;
            var sheetMid = (minU + maxU) * 0.5;

            if (tbMinU > sheetMid)
            {
                // Right-side title block
                maxU = Math.Min(maxU, tbMinU);
            }
            else if (tbMaxU < sheetMid)
            {
                // Left-side title block
                minU = Math.Max(minU, tbMaxU);
            }
            else
            {
                // Full-bleed / border title block — inset using the inner empty area heuristic.
                var reserve = Math.Clamp(settings.TitleBlockRightReserve, 0.15, 0.45);
                maxU = minU + (outline.Max.U - outline.Min.U) * (1.0 - reserve);
            }
        }
        else
        {
            var reserve = Math.Clamp(settings.TitleBlockRightReserve, 0.15, 0.45);
            maxU = minU + (outline.Max.U - outline.Min.U) * (1.0 - reserve);
        }

        var margin = Math.Max(0.1, settings.SheetMarginFt);
        minU += margin;
        maxU -= margin;
        minV += margin;
        maxV -= margin;

        if (maxU <= minU + 0.5)
        {
            minU = outline.Min.U + margin;
            maxU = outline.Min.U + Math.Max(outline.Max.U - outline.Min.U, 2) * 0.65;
        }

        if (maxV <= minV + 0.5)
        {
            minV = outline.Min.V + margin;
            maxV = outline.Max.V - margin;
        }

        return new BoundingBoxUV(minU, minV, maxU, maxV);
    }

    public static BoundingBoxXYZ? GetTitleBlockBoundingBox(ViewSheet sheet)
    {
        BoundingBoxXYZ? union = null;
        foreach (var inst in TitleBlockService.GetTitleBlockInstances(sheet.Document, sheet))
        {
            var bb = inst.get_BoundingBox(sheet);
            if (bb == null) continue;
            union = union == null ? CloneBox(bb) : Union(union, bb);
        }

        return union;
    }

    /// <summary>Axis-aligned model-space bounds of detail curves + text notes in a view.</summary>
    public static bool TryGetContentBounds(Document doc, Autodesk.Revit.DB.View view, out BoundingBoxXYZ bounds)
    {
        bounds = null!;
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        var found = false;

        foreach (var curve in new FilteredElementCollector(doc, view.Id)
                     .OfClass(typeof(CurveElement))
                     .Cast<CurveElement>())
        {
            var c = curve.GeometryCurve;
            if (c == null) continue;
            Expand(c.GetEndPoint(0));
            Expand(c.GetEndPoint(1));
            found = true;
        }

        foreach (var note in new FilteredElementCollector(doc, view.Id)
                     .OfClass(typeof(TextNote))
                     .Cast<TextNote>())
        {
            var bb = note.get_BoundingBox(view);
            if (bb == null) continue;
            Expand(bb.Min);
            Expand(bb.Max);
            found = true;
        }

        if (!found) return false;

        bounds = new BoundingBoxXYZ
        {
            Min = new XYZ(minX, minY, 0),
            Max = new XYZ(maxX, maxY, 0)
        };
        return true;

        void Expand(XYZ p)
        {
            minX = Math.Min(minX, p.X);
            minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X);
            maxY = Math.Max(maxY, p.Y);
        }
    }

    public static int FitScale(double contentWidthFt, double contentHeightFt, double paperWidthFt, double paperHeightFt, int preferredOrZero)
    {
        if (preferredOrZero > 0)
            return preferredOrZero;

        contentWidthFt = Math.Max(contentWidthFt, 0.5);
        contentHeightFt = Math.Max(contentHeightFt, 0.5);
        paperWidthFt = Math.Max(paperWidthFt, 0.5);
        paperHeightFt = Math.Max(paperHeightFt, 0.5);

        // Leave ~10% breathing room inside the printable area.
        var usableW = paperWidthFt * 0.90;
        var usableH = paperHeightFt * 0.90;

        var needed = Math.Max(contentWidthFt / usableW, contentHeightFt / usableH);
        var scale = (int)Math.Ceiling(needed);
        return SnapScale(Math.Max(scale, 1));
    }

    public static int SnapScale(int scale)
    {
        foreach (var s in StandardScales)
        {
            if (s >= scale) return s;
        }

        return StandardScales[^1];
    }

    public static XYZ CenterOf(BoundingBoxUV area) =>
        new((area.Min.U + area.Max.U) * 0.5, (area.Min.V + area.Max.V) * 0.5, 0);

    public static void PlaceViewport(
        Document doc,
        ViewSheet sheet,
        ElementId viewId,
        BoundingBoxUV printable,
        double uFraction,
        double vFraction)
    {
        var origin = new XYZ(
            printable.Min.U + (printable.Max.U - printable.Min.U) * uFraction,
            printable.Min.V + (printable.Max.V - printable.Min.V) * vFraction,
            0);

        var vp = Viewport.Create(doc, sheet.Id, viewId, origin);
        try
        {
            vp.SetBoxCenter(origin);
        }
        catch
        {
            // Some viewport types reject SetBoxCenter; Create origin is enough.
        }
    }

    private static BoundingBoxXYZ CloneBox(BoundingBoxXYZ bb) =>
        new() { Min = bb.Min, Max = bb.Max };

    private static BoundingBoxXYZ Union(BoundingBoxXYZ a, BoundingBoxXYZ b) =>
        new()
        {
            Min = new XYZ(Math.Min(a.Min.X, b.Min.X), Math.Min(a.Min.Y, b.Min.Y), Math.Min(a.Min.Z, b.Min.Z)),
            Max = new XYZ(Math.Max(a.Max.X, b.Max.X), Math.Max(a.Max.Y, b.Max.Y), Math.Max(a.Max.Z, b.Max.Z))
        };
}
