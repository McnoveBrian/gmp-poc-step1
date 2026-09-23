using Autodesk.Revit.DB;
using GmpPlatform.Core.Logging;
using GmpPlatform.Core.Settings;

namespace GmpPlatform.Revit.ShopTickets;

/// <summary>Applies / replaces title-block instances on sheets.</summary>
internal static class TitleBlockService
{
    /// <summary>
    /// Replaces the title block on every sheet this add-in created — and only those.
    /// <para>
    /// Writes use the extensible-storage marker alone, never the name heuristics in
    /// <see cref="IsShopTicketSheet"/>. Those heuristics are fine for finding sheets to look at;
    /// they are not fine for deciding which of the client's sheets to delete a title block
    /// from, because any GMP sheet whose name reads " TIF" would match. Sheets generated
    /// before the marker existed are therefore left alone here; that is the right way round
    /// for a mistake to fall.
    /// </para>
    /// </summary>
    public static int ApplyToShopTicketSheets(Document doc, ElementId titleBlockTypeId)
    {
        var sheets = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>()
            .Where(ShopTicketMarker.IsOurs)
            .ToList();

        return sheets.Count == 0 ? 0 : ApplyToSheets(doc, sheets, titleBlockTypeId);
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
        // An explicit claim beats any amount of inference: sheets this add-in creates carry a
        // marker in extensible storage (see ShopTicketMarker). Everything below is the legacy
        // fallback for sheets generated before the marker existed, and it is a guess — it will
        // happily match a GMP sheet that merely reads " TIF".
        if (ShopTicketMarker.IsOurs(sheet)) return true;

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

    /// <summary>
    /// The title block for a shop ticket. Goes through the same resolver as the confirmation
    /// dialog, so the dialog can never promise one title block while the sheets get another.
    /// </summary>
    public static ElementId ResolveOrDefault(Document doc, PlatformSettings settings) =>
        TitleBlockCatalog.ResolveForShopTicket(doc, settings, out _);
}
