using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using GmpPlatform.Core;
using GmpPlatform.Core.Logging;

namespace GmpPlatform.Revit.Commands;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class ExportShopTicketDwgCommand : ExternalCommand
{
    public override void Execute()
    {
        var uidoc = Application.ActiveUIDocument
                     ?? throw new InvalidOperationException("No active document.");
        var doc = uidoc.Document;
        var view = doc.ActiveView;

        if (view is not ViewSheet && view is not ViewDrafting && view is not ViewSection)
        {
            TaskDialog.Show(
                "GMP Platform",
                "Open a shop-ticket sheet (or one of its drafting/section views), then run Export DWG.");
            return;
        }

        PlatformPaths.EnsureDirectories();
        var folder = Path.Combine(PlatformPaths.Exports, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(folder);

        var views = new List<ElementId> { view.Id };
        if (view is ViewSheet sheet)
        {
            // Prefer exporting the sheet itself
            views = new List<ElementId> { sheet.Id };
        }

        var options = new DWGExportOptions
        {
            FileVersion = ACADVersion.Default,
            MergedViews = true,
            ExportOfSolids = SolidGeometry.ACIS
        };

        var name = SanitizeFileName(view.Name);
        try
        {
            var ok = doc.Export(folder, name, views, options);
            PlatformLog.Logger.Information("DWG export {Ok} to {Folder}\\{Name}", ok, folder, name);
            TaskDialog.Show(
                "GMP Platform — Export DWG",
                ok
                    ? $"Exported to:\n{folder}\n\nNote: client dimension/text style matching is a later standards-bridge step."
                    : "Export returned false. Check that DWG export is available in this Revit build.");
        }
        catch (Exception ex)
        {
            PlatformLog.Logger.Error(ex, "DWG export failed");
            TaskDialog.Show("GMP Platform — Export Error", ex.Message);
        }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "GMP_ShopTicket" : name;
    }
}
