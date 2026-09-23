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

    /// <summary>
    /// The best guess at a production title block when nothing has been chosen.
    /// <para>
    /// Callers used to take <c>List(doc).First()</c> and save it, so one arbitrary pick became
    /// permanent — and on the Pilgrim's RTE model the first entry is
    /// <c>R25_GMP Project Cover Page</c>. Every shop ticket then came out on a cover page,
    /// complete with the GMP logo and "Project Team" fields, with the panel drawn over them.
    /// </para>
    /// <para>
    /// Ranking on the name is still a guess, but a guess that prefers a sheet meant for
    /// production over one meant for the front of a set. It returns null rather than something
    /// arbitrary when every candidate looks wrong, so the caller can say so.
    /// </para>
    /// </summary>
    public static TitleBlockOption? BestGuess(Document doc)
    {
        var options = List(doc);
        if (options.Count == 0) return null;
        if (options.Count == 1) return options[0];

        string[] wanted = { "TICKET", "SHOP", "PRODUCTION", "PRECAST", "DETAIL" };
        string[] unwanted = { "COVER", "TITLE SHEET", "TITLESHEET", "INDEX", "LEGEND", "PRESENTATION" };

        int Rank(TitleBlockOption o)
        {
            var name = (o.DisplayName ?? "").ToUpperInvariant();
            if (unwanted.Any(name.Contains)) return -1;
            return wanted.Any(name.Contains) ? 1 : 0;
        }

        var best = options
            .Select(o => new { Option = o, Rank = Rank(o) })
            .OrderByDescending(x => x.Rank)
            .First();

        // Everything available looks like front matter: say nothing rather than pick one.
        return best.Rank < 0 ? null : best.Option;
    }

    /// <summary>
    /// The title block to put on a shop ticket, with a sentence explaining the choice so the
    /// confirmation dialog can show it.
    /// <para>
    /// A saved preference normally wins, but not when it is a cover page: that preference was
    /// most likely saved by the old first-in-the-list auto-pick rather than chosen by anyone,
    /// and honouring it puts every ticket on front matter.
    /// </para>
    /// </summary>
    public static ElementId ResolveForShopTicket(Document doc, PlatformSettings settings, out string note)
    {
        var preferred = ResolvePreferredId(doc, settings);
        if (preferred != ElementId.InvalidElementId && !LooksLikeCoverPage(doc, preferred))
        {
            note = "From Settings.";
            return preferred;
        }

        var best = BestGuess(doc);
        if (best != null)
        {
            note = preferred != ElementId.InvalidElementId
                ? $"The saved choice, {Describe(doc, preferred)}, is a cover page, so a production " +
                  "title block was chosen instead. Change it in Settings if this is wrong."
                : "Chosen automatically. Change it in Settings if this is wrong.";
            return best.SymbolId;
        }

        note = "No suitable production title block was found in this model. Choose one in Settings.";
        return ElementId.InvalidElementId;
    }

    /// <summary>True when this title block looks like front matter rather than a work sheet.</summary>
    public static bool LooksLikeCoverPage(Document doc, ElementId id)
    {
        if (id == ElementId.InvalidElementId) return false;
        var name = (Describe(doc, id) ?? "").ToUpperInvariant();
        return name.Contains("COVER") || name.Contains("TITLE SHEET") || name.Contains("INDEX");
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
