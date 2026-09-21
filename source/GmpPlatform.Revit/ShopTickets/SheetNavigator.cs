using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace GmpPlatform.Revit.ShopTickets;

/// <summary>Opens generated sheets as document tabs so the user can review them immediately.</summary>
internal static class SheetNavigator
{
    public static void OpenSheets(UIDocument uidoc, IEnumerable<long> sheetElementIds)
    {
        var doc = uidoc.Document;
        ViewSheet? first = null;

        foreach (var idValue in sheetElementIds)
        {
            var sheet = doc.GetElement(new ElementId(idValue)) as ViewSheet;
            if (sheet == null || sheet.IsTemplate) continue;

            try
            {
                // Assigning ActiveView opens the sheet as a tab if it is not already open.
                uidoc.ActiveView = sheet;
                first ??= sheet;
            }
            catch
            {
                try
                {
                    uidoc.RequestViewChange(sheet);
                    first ??= sheet;
                }
                catch
                {
                    // Skip sheets that cannot be activated (rare).
                }
            }
        }

        // Leave the first sheet (TIF) active for review.
        if (first != null)
        {
            try { uidoc.ActiveView = first; }
            catch { /* ignore */ }
        }
    }
}
