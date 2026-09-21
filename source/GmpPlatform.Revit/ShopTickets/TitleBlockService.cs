using Autodesk.Revit.DB;
using GmpPlatform.Core.Logging;
using GmpPlatform.Core.Settings;

namespace GmpPlatform.Revit.ShopTickets;

/// <summary>Applies / replaces title-block instances on sheets.</summary>
internal static class TitleBlockService
{
    public static int ApplyToShopTicketSheets(Document doc, ElementId titleBlockTypeId)
    {
        var sheets = FindShopTicketSheets(doc).ToList();
        if (sheets.Count == 0)
            return 0;

        return ApplyToSheets(doc, sheets, titleBlockTypeId);
    }

    public static int ApplyToSheets(Document doc, IEnumerable<ViewSheet> sheets, ElementId titleBlockTypeId)
    {
        if (titleBlockTypeId == ElementId.InvalidElementId)
            return 0;

        if (doc.GetElement(titleBlockTypeId) is not FamilySymbol symbol)
            return 0;

        if (!symbol.IsActive)
            symbol.Activate();

        var count = 0;
        foreach (var sheet in sheets)
        {
            if (ReplaceTitleBlock(doc, sheet, symbol))
                count++;
        }

        PlatformLog.Logger.Information(
            "Applied title block {Type} to {Count} sheet(s)",
            $"{symbol.FamilyName} : {symbol.Name}",
            count);

        return count;
    }

    /// <summary>
    /// Place the preferred title block on a newly created blank sheet.
    /// Prefer this over ViewSheet.Create(doc, typeId) so every sheet gets its own instance.
    /// </summary>
    public static FamilyInstance? PlaceOnSheet(Document doc, ViewSheet sheet, ElementId titleBlockTypeId)
    {
        if (titleBlockTypeId == ElementId.InvalidElementId)
            return null;

        if (doc.GetElement(titleBlockTypeId) is not FamilySymbol symbol)
            return null;

        if (!symbol.IsActive)
            symbol.Activate();

        // Remove any auto-placed leftovers first.
        DeleteTitleBlocksOnSheet(doc, sheet);

        return doc.Create.NewFamilyInstance(XYZ.Zero, symbol, sheet);
    }

    public static bool ReplaceTitleBlock(Document doc, ViewSheet sheet, FamilySymbol symbol)
    {
        var existing = GetTitleBlockInstances(doc, sheet).ToList();

        // Fast path: change type on existing instance(s).
        if (existing.Count > 0)
        {
            foreach (var inst in existing)
            {
                if (inst.GetTypeId() != symbol.Id)
                    inst.ChangeTypeId(symbol.Id);
            }

            return true;
        }

        doc.Create.NewFamilyInstance(XYZ.Zero, symbol, sheet);
        return true;
    }

    public static void DeleteTitleBlocksOnSheet(Document doc, ViewSheet sheet)
    {
        var ids = GetTitleBlockInstances(doc, sheet).Select(e => e.Id).ToList();
        if (ids.Count > 0)
            doc.Delete(ids);
    }

    public static IEnumerable<FamilyInstance> GetTitleBlockInstances(Document doc, ViewSheet sheet)
    {
        return new FilteredElementCollector(doc, sheet.Id)
            .OfCategory(BuiltInCategory.OST_TitleBlocks)
            .WhereElementIsNotElementType()
            .OfClass(typeof(FamilyInstance))
            .Cast<FamilyInstance>();
    }

    public static IEnumerable<ViewSheet> FindShopTicketSheets(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>()
            .Where(IsShopTicketSheet);
    }

    public static bool IsShopTicketSheet(ViewSheet sheet)
    {
        var name = sheet.Name ?? "";
        var number = sheet.SheetNumber ?? "";

        // Fast path: naming conventions from our generator.
        if (name.Contains(" TIF", StringComparison.OrdinalIgnoreCase) ||
            name.Contains(" BIF", StringComparison.OrdinalIgnoreCase) ||
            name.Contains(" SECTIONS", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(" TIF", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(" BIF", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(" SECTIONS", StringComparison.OrdinalIgnoreCase))
            return true;

        // Avoid scanning every viewport on every sheet in large models.
        if (!(number.Contains('-') &&
              (number.EndsWith("-1", StringComparison.Ordinal) ||
               number.EndsWith("-2", StringComparison.Ordinal) ||
               number.EndsWith("-3", StringComparison.Ordinal) ||
               number.Contains("-2-", StringComparison.Ordinal) ||
               number.Contains("-3-", StringComparison.Ordinal))))
            return false;

        var doc = sheet.Document;
        foreach (var vpId in sheet.GetAllViewports())
        {
            if (doc.GetElement(vpId) is not Viewport vp) continue;
            if (doc.GetElement(vp.ViewId) is not Autodesk.Revit.DB.View view) continue;
            var n = view.Name ?? "";
            if (n.Contains("_TifFabrication", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("_BifFinish", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("_SectionsAndBom", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static ElementId ResolveOrDefault(Document doc, PlatformSettings settings) =>
        TitleBlockCatalog.ResolvePreferredId(doc, settings);
}
