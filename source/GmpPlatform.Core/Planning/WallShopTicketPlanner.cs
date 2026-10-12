using GmpPlatform.Core.Models;

namespace GmpPlatform.Core.Planning;

/// <summary>
/// Builds the 3-sheet plan for the retired Revit-sheet route (Developer tools only).
/// Production tickets use the AutoCAD engine and <c>PieceTicket</c>; do not extend this
/// planner for product features.
/// </summary>
public static class WallShopTicketPlanner
{
    public static ShopTicketPlan CreatePlan(WallPanelSnapshot panel)
    {
        var mark = string.IsNullOrWhiteSpace(panel.Mark) ? "UNMARKED" : panel.Mark.Trim();
        var runId = $"GMP-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(panel.Mark))
            warnings.Add("Mark parameter was empty; using UNMARKED as the sheet mark.");

        if (panel.NeighborMarks.Count == 0)
            warnings.Add("No neighboring panels detected. Joint Detail Lines will use a placeholder.");

        if (panel.Parts.Count == 0)
            warnings.Add("No parts resolved from the model. Sheet 3 BOM will be empty rather than invented.");

        // Anything the reader could not resolve has to travel with the plan; a ticket that
        // looks complete but rests on missing data is worse than one that says so.
        warnings.AddRange(panel.DataWarnings);

        var sheets = new List<ShopTicketSheetPlan>
        {
            new()
            {
                Kind = ShopTicketSheetKind.TifFabrication,
                SheetNumber = $"{mark}-1",
                SheetName = $"{mark} TIF",
                Title = "TIF VIEW — Top In Form (developer route)",
                PlannedViews = new[] { "TIF drafting layout", "Section through panel (if available)" },
                ProductionNotes = new[]
                {
                    "ROCK ALL TIF EDGES.",
                    "SEE SHEET 2 FOR BIF VIEW (CONCRETE REMOVED).",
                    "SEE SHEET 3 FOR DETAILS AND BOM.",
                    "DEV: connection Detail Lines are illustrative pending GMP joint rules."
                }
            },
            new()
            {
                Kind = ShopTicketSheetKind.BifFinish,
                SheetNumber = $"{mark}-2",
                SheetName = $"{mark} BIF",
                Title = "BOTTOM FINISH LAYOUT — CONCRETE REMOVED (developer route)",
                PlannedViews = new[] { "BIF finish drafting layout" },
                ProductionNotes = new[]
                {
                    "ROCK ALL T/F EDGES.",
                    "SEE SHEET 1 FOR TOP IN FORM VIEW.",
                    "SEE SHEET 3 FOR DETAILS.",
                    "DEV: edge details G–U are placeholders."
                }
            },
            new()
            {
                Kind = ShopTicketSheetKind.SectionsAndBom,
                SheetNumber = $"{mark}-3",
                SheetName = $"{mark} SECTIONS",
                Title = "SECTIONS + BILL OF MATERIAL (developer route)",
                PlannedViews = new[] { "Sections drafting layout", "BOM table" },
                ProductionNotes = new[]
                {
                    "Grade 'A' Finish (placeholder).",
                    "AS — Light sandblast per approved sample (placeholder).",
                    "Section cut locations follow GMP rules on the AutoCAD Ticket Drawing path."
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
