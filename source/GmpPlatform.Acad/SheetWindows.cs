using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace GmpPlatform.Acad;

/// <summary>The model-space rectangle a paper-space viewport shows.</summary>
internal readonly record struct ViewWindow(
    double CenterX, double CenterY, double Width, double Height, double Scale,
    ObjectId ViewportId = default, double PaperWidth = 0, double PaperHeight = 0)
{
    /// <summary>Model-space scale factor, e.g. 64 for 3/16" = 1'-0". 0 when unknown.</summary>
    public double AnnotationScale => Scale > 0 ? Math.Round(1.0 / Scale) : 0;

    public double MinX => CenterX - Width / 2.0;
    public double MaxX => CenterX + Width / 2.0;
    public double MinY => CenterY - Height / 2.0;
    public double MaxY => CenterY + Height / 2.0;

    public bool Contains(Point3d p) =>
        p.X >= MinX && p.X <= MaxX && p.Y >= MinY && p.Y <= MaxY;
}

internal sealed record SheetWindow(string Layout, int Viewports, ViewWindow? Main, IReadOnlyList<ViewWindow> All);

/// <summary>
/// Finds what each layout's viewports look at, so generated content can be drawn into the
/// same model-space coordinates the client's existing sheets already frame.
/// </summary>
internal static class SheetWindows
{
    public static IReadOnlyList<SheetWindow> Discover(Database db, Transaction tr)
    {
        var results = new List<SheetWindow>();
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

        foreach (ObjectId btrId in bt)
        {
            var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
            if (!btr.IsLayout) continue;

            var layout = (Layout)tr.GetObject(btr.LayoutId, OpenMode.ForRead);
            if (layout.ModelType) continue;

            var windows = new List<ViewWindow>();
            foreach (ObjectId id in btr)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is not Viewport vp) continue;

                // Viewport 1 is the paper sheet itself, not a drawing view.
                if (vp.Number == 1) continue;
                if (vp.Height <= 0 || vp.ViewHeight <= 0) continue;

                var height = vp.ViewHeight;
                var width = height * (vp.Width / vp.Height);

                // The stored view centre is relative to the view target.
                var cx = vp.ViewCenter.X + vp.ViewTarget.X;
                var cy = vp.ViewCenter.Y + vp.ViewTarget.Y;
                windows.Add(new ViewWindow(cx, cy, width, height, vp.CustomScale, vp.ObjectId, vp.Width, vp.Height));
            }

            // The main drawing view is the largest window on the sheet.
            ViewWindow? main = windows.Count == 0
                ? null
                : windows.OrderByDescending(w => w.Width * w.Height).First();

            results.Add(new SheetWindow(layout.LayoutName, windows.Count, main, windows));
        }

        return results
            .OrderBy(r => r.Layout, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
