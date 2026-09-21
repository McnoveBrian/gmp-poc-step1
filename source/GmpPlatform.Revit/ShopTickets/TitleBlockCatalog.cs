using Autodesk.Revit.DB;
using GmpPlatform.Core.Settings;

namespace GmpPlatform.Revit.ShopTickets;

internal sealed record TitleBlockOption(ElementId SymbolId, string UniqueId, string DisplayName);

/// <summary>Discovers title-block types in the active document and resolves the saved preference.</summary>
internal static class TitleBlockCatalog
{
    public static IReadOnlyList<TitleBlockOption> List(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_TitleBlocks)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .OrderBy(s => s.FamilyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => new TitleBlockOption(
                s.Id,
                s.UniqueId,
                $"{s.FamilyName} : {s.Name}"))
            .ToList();
    }

    public static ElementId ResolvePreferredId(Document doc, PlatformSettings settings)
    {
        var options = List(doc);
        if (options.Count == 0)
            return ElementId.InvalidElementId;

        if (!string.IsNullOrWhiteSpace(settings.TitleBlockUniqueId))
        {
            var byId = options.FirstOrDefault(o =>
                string.Equals(o.UniqueId, settings.TitleBlockUniqueId, StringComparison.OrdinalIgnoreCase));
            if (byId != null)
                return byId.SymbolId;
        }

        if (!string.IsNullOrWhiteSpace(settings.TitleBlockFamilyAndType))
        {
            var byName = options.FirstOrDefault(o =>
                string.Equals(o.DisplayName, settings.TitleBlockFamilyAndType, StringComparison.OrdinalIgnoreCase));
            if (byName != null)
                return byName.SymbolId;
        }

        // No preference saved — leave blank so Settings must be used deliberately,
        // unless the document has exactly one title block (auto-pick).
        return options.Count == 1 ? options[0].SymbolId : ElementId.InvalidElementId;
    }

    public static string Describe(Document doc, ElementId id)
    {
        if (id == ElementId.InvalidElementId)
            return "(none — blank sheet)";

        if (doc.GetElement(id) is FamilySymbol symbol)
            return $"{symbol.FamilyName} : {symbol.Name}";

        return "(missing title block)";
    }
}
