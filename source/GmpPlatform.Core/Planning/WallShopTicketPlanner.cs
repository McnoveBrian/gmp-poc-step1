using GmpPlatform.Core.Models;

namespace GmpPlatform.Core.Planning;

/// <summary>
/// Builds the 3-sheet shop-ticket plan for a simple rectangular wall panel (demo rules).
/// Later phases replace hard-coded notes with profile-driven rules.
/// </summary>
public static class WallShopTicketPlanner
{
    public static ShopTicketPlan CreatePlan(WallPanelSnapshot panel)
    {
        var mark = string.IsNullOrWhiteSpace(panel.Mark) ? "DEMO-WALL" : panel.Mark.Trim();
        var runId = $"GMP-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(panel.Mark))
            warnings.Add("Mark parameter was empty; using DEMO-WALL as the sheet mark.");

        if (panel.NeighborMarks.Count == 0)
            warnings.Add("No neighboring panels detected. Joint Detail Lines will use a demo placeholder.");

        if (panel.Parts.Count == 0)
            warnings.Add("No nested part quantities found. Sheet 3 BOM will show demo placeholder rows.");

        var sheets = new List<ShopTicketSheetPlan>
        {
            new()
            {
                Kind = ShopTicketSheetKind.TifFabrication,
                SheetNumber = $"{mark}-1",
                SheetName = $"{mark} TIF",
                Title = "TIF VIEW — Top In Form (Demo)",
                PlannedViews = new[] { "TIF drafting layout", "Section through panel (if available)" },
                ProductionNotes = new[]
                {
                    "ROCK ALL TIF EDGES.",
                    "SEE SHEET 2 FOR BIF VIEW (CONCRETE REMOVED).",
                    "SEE SHEET 3 FOR DETAILS AND BOM.",
                    "DEMO: connection Detail Lines are illustrative pending GMP joint rules."
                }
            },
            new()
            {
                Kind = ShopTicketSheetKind.BifFinish,
                SheetNumber = $"{mark}-2",
                SheetName = $"{mark} BIF",
                Title = "BOTTOM FINISH LAYOUT — CONCRETE REMOVED (Demo)",
                PlannedViews = new[] { "BIF finish drafting layout" },
                ProductionNotes = new[]
                {
                    "ROCK ALL T/F EDGES.",
                    "SEE SHEET 1 FOR TOP IN FORM VIEW.",
                    "SEE SHEET 3 FOR DETAILS.",
                    "DEMO: edge details G–U are placeholders."
                }
            },
            new()
            {
                Kind = ShopTicketSheetKind.SectionsAndBom,
                SheetNumber = $"{mark}-3",
                SheetName = $"{mark} SECTIONS",
                Title = "SECTIONS + BILL OF MATERIAL (Demo)",
                PlannedViews = new[] { "Sections drafting layout", "BOM table" },
                ProductionNotes = new[]
                {
                    "Grade 'A' Finish (demo note).",
                    "AS — Light sandblast per approved sample (demo note).",
                    "Section cut locations will follow GMP rules in the full build."
                }
            }
        };

        return new ShopTicketPlan
        {
            RunId = runId,
            CreatedUtc = DateTimeOffset.UtcNow,
            Panel = panel,
            Sheets = sheets,
            Warnings = warnings
        };
    }
}
