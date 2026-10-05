namespace GmpPlatform.Core.Tickets;

/// <summary>A paper-space layout of the seed drawing, as far as sheet roles care.</summary>
public sealed class SheetCandidate
{
    public SheetCandidate(string layout, int tabOrder, bool hasMainView, int windows)
    {
        Layout = layout;
        TabOrder = tabOrder;
        HasMainView = hasMainView;
        Windows = windows;
    }

    /// <summary>Layout (tab) name.</summary>
    public string Layout { get; }

    /// <summary>Position of the tab, as the drafter sees it.</summary>
    public int TabOrder { get; }

    /// <summary>Whether it has a viewport that can hold a piece view.</summary>
    public bool HasMainView { get; }

    /// <summary>How many drawing viewports it has.</summary>
    public int Windows { get; }
}

/// <summary>Which layout carries which part of the ticket.</summary>
public sealed class SheetRoleAssignment
{
    /// <summary>Top-in-form view (sheet 1 on the approved ticket).</summary>
    public string? Tif { get; set; }

    /// <summary>Bottom-in-form view (sheet 2).</summary>
    public string? Bif { get; set; }

    /// <summary>Sections (sheet 3).</summary>
    public string? Sections { get; set; }

    /// <summary>Configured names that could not be honoured, for the draw log.</summary>
    public List<string> Warnings { get; } = new();
}

/// <summary>
/// Decides which seed layout gets the TIF view, the BIF view and the sections.
/// <para>
/// Layout names configured in settings win. Without them, roles follow the <b>tab order</b>:
/// the first two layouts that can hold a piece view are TIF then BIF, and the next layout with
/// viewports is the sections sheet. Tab order is what the drafter sees; the alphabetical order
/// used before only worked while the tabs happened to be named "1 of 3", "2 of 3", "3 of 3",
/// and would put "Sheet 10" before "Sheet 2" or "BIF" before "TIF".
/// </para>
/// </summary>
public static class SheetRoles
{
    public static SheetRoleAssignment Assign(
        IEnumerable<SheetCandidate> layouts,
        string? tifLayout = null,
        string? bifLayout = null,
        string? sectionsLayout = null)
    {
        var ordered = layouts.OrderBy(l => l.TabOrder).ToList();
        var result = new SheetRoleAssignment();
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string? Configured(string? wanted, string role, Func<SheetCandidate, bool> usable)
        {
            if (string.IsNullOrWhiteSpace(wanted)) return null;
            var match = ordered.FirstOrDefault(l =>
                string.Equals(l.Layout.Trim(), wanted!.Trim(), StringComparison.OrdinalIgnoreCase));

            if (match == null)
                result.Warnings.Add($"{role} layout '{wanted}' is not in the seed drawing; chosen by tab order instead.");
            else if (!usable(match))
                result.Warnings.Add($"{role} layout '{wanted}' has no suitable viewport; chosen by tab order instead.");
            else if (taken.Contains(match.Layout))
                result.Warnings.Add($"{role} layout '{wanted}' is already used for another role; chosen by tab order instead.");
            else
            {
                taken.Add(match.Layout);
                return match.Layout;
            }
            return null;
        }

        bool PieceView(SheetCandidate l) => l.HasMainView;
        bool SectionSheet(SheetCandidate l) => l.Windows > 0;

        result.Tif = Configured(tifLayout, "TIF", PieceView);
        result.Bif = Configured(bifLayout, "BIF", PieceView);
        result.Sections = Configured(sectionsLayout, "Sections", SectionSheet);

        string? Next(Func<SheetCandidate, bool> usable)
        {
            var next = ordered.FirstOrDefault(l => usable(l) && !taken.Contains(l.Layout));
            if (next != null) taken.Add(next.Layout);
            return next?.Layout;
        }

        result.Tif ??= Next(PieceView);
        result.Bif ??= Next(PieceView);
        result.Sections ??= Next(SectionSheet);
        return result;
    }
}
