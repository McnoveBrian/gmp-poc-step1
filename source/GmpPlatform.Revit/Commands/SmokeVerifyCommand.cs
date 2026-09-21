using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using GmpPlatform.Core;
using GmpPlatform.Core.Logging;
using GmpPlatform.Core.Planning;
using GmpPlatform.Core.Reports;
using GmpPlatform.Core.Settings;
using GmpPlatform.Revit.ShopTickets;

namespace GmpPlatform.Revit.Commands;

/// <summary>
/// Quiet end-to-end verification: pick a panel, generate sheets, assert title blocks + scales.
/// Set env GMP_SMOKE_SILENT=1 to skip dialogs (for journal automation).
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class SmokeVerifyCommand : ExternalCommand
{
    public override void Execute()
    {
        var silent = string.Equals(
            Environment.GetEnvironmentVariable("GMP_SMOKE_SILENT"),
            "1",
            StringComparison.Ordinal);

        var uidoc = Application.ActiveUIDocument;
        if (uidoc == null)
        {
            Report(silent, false, "No active document.");
            return;
        }

        var doc = uidoc.Document;
        var settings = SettingsStore.Load();
        var titleBlockId = TitleBlockCatalog.ResolvePreferredId(doc, settings);

        Element panel;
        if (!WallPanelReader.TryGetSelectedPanel(uidoc, out panel!, out _))
        {
            panel = FindAnyPanel(doc)!;
            if (panel == null)
            {
                Report(silent, false, "No wall panel selected and none found in the model.");
                return;
            }

            uidoc.Selection.SetElementIds(new List<ElementId> { panel.Id });
        }

        try
        {
            var snapshot = WallPanelReader.Read(panel);
            var plan = WallShopTicketPlanner.CreatePlan(snapshot);
            var generator = new WallShopTicketGenerator(doc, panel, plan, settings);
            var result = generator.Generate();
            var reportFolder = RunReportWriter.Write(plan, result);

            var issues = new List<string>();
            var sheets = TitleBlockService.FindShopTicketSheets(doc)
                .Where(s => result.CreatedSheetNames.Any(n =>
                    n.StartsWith(s.SheetNumber + " ", StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith(s.SheetNumber + "—", StringComparison.OrdinalIgnoreCase) ||
                    n.Contains(s.SheetNumber, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (sheets.Count < 3)
            {
                // Fall back: sheets created in this run by number prefix.
                sheets = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Where(s => result.CreatedSheetNames.Any(n => n.Contains(s.SheetNumber)))
                    .ToList();
            }

            foreach (var sheet in sheets)
            {
                var tbs = TitleBlockService.GetTitleBlockInstances(doc, sheet).ToList();
                if (titleBlockId != ElementId.InvalidElementId && tbs.Count == 0)
                    issues.Add($"{sheet.SheetNumber}: missing title block instance");
                else if (titleBlockId != ElementId.InvalidElementId &&
                         tbs.Any(t => t.GetTypeId() != titleBlockId))
                    issues.Add($"{sheet.SheetNumber}: title block type mismatch");

                foreach (var vpId in sheet.GetAllViewports())
                {
                    if (doc.GetElement(vpId) is not Viewport vp) continue;
                    if (doc.GetElement(vp.ViewId) is not Autodesk.Revit.DB.View view) continue;
                    if (view.Scale <= 0)
                        issues.Add($"{sheet.SheetNumber}/{view.Name}: invalid scale");
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine(result.Message);
            sb.AppendLine($"Sheets checked: {sheets.Count}");
            sb.AppendLine($"Title block: {TitleBlockCatalog.Describe(doc, titleBlockId)}");
            sb.AppendLine($"Report: {reportFolder}");
            if (issues.Count == 0)
                sb.AppendLine("VERIFY PASS: title blocks present on sheets; view scales set.");
            else
            {
                sb.AppendLine("VERIFY ISSUES:");
                foreach (var issue in issues)
                    sb.AppendLine("• " + issue);
            }

            var ok = issues.Count == 0 && result.Success;
            WriteSmokeFile(ok, sb.ToString());
            Report(silent, ok, sb.ToString());
        }
        catch (Exception ex)
        {
            PlatformLog.Logger.Error(ex, "Smoke verify failed");
            WriteSmokeFile(false, ex.ToString());
            Report(silent, false, ex.Message);
        }
    }

    private static Element? FindAnyPanel(Document doc)
    {
        return new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .FirstOrDefault(e =>
            {
                var c = (int)(e.Category?.Id.Value ?? 0);
                return c == (int)BuiltInCategory.OST_StructuralFraming ||
                       c == (int)BuiltInCategory.OST_Walls;
            });
    }

    private static void WriteSmokeFile(bool ok, string body)
    {
        try
        {
            PlatformPaths.EnsureDirectories();
            var path = Path.Combine(PlatformPaths.Reports, "smoke-verify.txt");
            File.WriteAllText(path, $"{(ok ? "PASS" : "FAIL")}\n{DateTimeOffset.Now:o}\n\n{body}");
        }
        catch
        {
            // Ignore IO errors during smoke.
        }
    }

    private static void Report(bool silent, bool ok, string message)
    {
        if (silent)
        {
            PlatformLog.Logger.Information("Smoke verify silent result ok={Ok}: {Message}", ok, message);
            return;
        }

        TaskDialog.Show(ok ? "GMP Platform — Verify PASS" : "GMP Platform — Verify FAIL", message);
    }
}
