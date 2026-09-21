using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Nice3point.Revit.Toolkit.External;
using GmpPlatform.Core;
using GmpPlatform.Core.Logging;
using GmpPlatform.Core.Planning;
using GmpPlatform.Core.Reports;
using GmpPlatform.Core.Settings;
using GmpPlatform.Revit.Ribbon;
using GmpPlatform.Revit.ShopTickets;

namespace GmpPlatform.Revit;

[UsedImplicitly]
public class Application : ExternalApplication
{
    private static bool _smokeArmed;
    private static bool _smokeRan;

    public override void OnStartup()
    {
        var version = Application.ControlledApplication.VersionNumber;
        PlatformLog.Initialize(version);
        PlatformLog.Logger.Information("GMP Production Platform starting. Revit {Version}", version);
        RibbonBuilder.Create(Application);

        _smokeArmed = string.Equals(
            Environment.GetEnvironmentVariable("GMP_SMOKE_SILENT"),
            "1",
            StringComparison.Ordinal);
        if (_smokeArmed)
        {
            PlatformLog.Logger.Information("GMP smoke mode armed — will verify after first document opens");
            Application.ControlledApplication.DocumentOpened += OnDocumentOpenedForSmoke;
        }
    }

    public override void OnShutdown()
    {
        if (_smokeArmed)
            Application.ControlledApplication.DocumentOpened -= OnDocumentOpenedForSmoke;

        PlatformLog.Logger.Information("GMP Production Platform shutting down");
        PlatformLog.Shutdown();
    }

    private static void OnDocumentOpenedForSmoke(object? sender, DocumentOpenedEventArgs e)
    {
        if (_smokeRan) return;
        _smokeRan = true;

        try
        {
            var doc = e.Document;
            if (doc == null || doc.IsFamilyDocument)
            {
                WriteSmoke(false, "Smoke skipped: no project document.");
                WriteDoneFlag();
                return;
            }

            PlatformLog.Logger.Information("GMP smoke starting on {Title}", doc.Title);
            var settings = SettingsStore.Load();

            var titleBlockId = TitleBlockCatalog.ResolvePreferredId(doc, settings);
            if (titleBlockId == ElementId.InvalidElementId)
            {
                var first = TitleBlockCatalog.List(doc).FirstOrDefault();
                if (first != null)
                {
                    settings.TitleBlockUniqueId = first.UniqueId;
                    settings.TitleBlockFamilyAndType = first.DisplayName;
                    settings.ViewScale = 0;
                    settings.ApplyTitleBlockToExistingSheets = true;
                    SettingsStore.Save(settings);
                    titleBlockId = first.SymbolId;
                }
            }

            var panel = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .FirstOrDefault(el =>
                {
                    var c = (int)(el.Category?.Id.Value ?? 0);
                    if (c != (int)BuiltInCategory.OST_StructuralFraming &&
                        c != (int)BuiltInCategory.OST_Walls)
                        return false;
                    var type = doc.GetElement(el.GetTypeId()) as ElementType;
                    var family = type is FamilySymbol fs
                        ? fs.FamilyName
                        : type?.Name ?? "";
                    return family.IndexOf("WALL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           (el.Category?.Name.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) >= 0);
                })
                ?? new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .FirstOrDefault(el =>
                    {
                        var c = (int)(el.Category?.Id.Value ?? 0);
                        return c == (int)BuiltInCategory.OST_StructuralFraming ||
                               c == (int)BuiltInCategory.OST_Walls;
                    });

            if (panel == null)
            {
                WriteSmoke(false, "Smoke failed: no Structural Framing / Wall element found.");
                WriteDoneFlag();
                return;
            }

            var snapshot = WallPanelReader.Read(panel);
            var plan = WallShopTicketPlanner.CreatePlan(snapshot);
            var generator = new WallShopTicketGenerator(doc, panel, plan, settings);
            var result = generator.Generate();
            var reportFolder = RunReportWriter.Write(plan, result);

            // Write an early result so a hung post-check cannot leave zero evidence.
            WriteSmoke(result.Success, $"GENERATE OK\n{result.Message}\nReport: {reportFolder}");

            var issues = new List<string>();
            var sheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => result.CreatedSheetNames.Any(n => n.Contains(s.SheetNumber)))
                .ToList();

            if (sheets.Count < 3)
                issues.Add($"Expected 3 sheets, found {sheets.Count}");

            foreach (var sheet in sheets)
            {
                var tbs = TitleBlockService.GetTitleBlockInstances(doc, sheet).ToList();
                if (titleBlockId != ElementId.InvalidElementId && tbs.Count == 0)
                    issues.Add($"{sheet.SheetNumber}: missing title block");
                else if (titleBlockId != ElementId.InvalidElementId &&
                         tbs.Any(t => t.GetTypeId() != titleBlockId))
                    issues.Add($"{sheet.SheetNumber}: title block type mismatch");

                var viewportCount = sheet.GetAllViewports().Count;
                if (viewportCount == 0)
                    issues.Add($"{sheet.SheetNumber}: no viewports");
            }

            // Lightweight re-apply only on the sheets we just created (not whole model scan).
            if (titleBlockId != ElementId.InvalidElementId && sheets.Count > 0)
            {
                using var tx = new Transaction(doc, "GMP smoke — reapply title blocks");
                tx.Start();
                var applied = TitleBlockService.ApplyToSheets(doc, sheets, titleBlockId);
                tx.Commit();
                if (applied < sheets.Count)
                    issues.Add($"Reapply touched {applied}/{sheets.Count} sheets");
            }

            var ok = issues.Count == 0 && result.Success;
            var body =
                $"{result.Message}\n" +
                $"Sheets: {string.Join(" | ", result.CreatedSheetNames)}\n" +
                $"Report: {reportFolder}\n" +
                $"Title block: {TitleBlockCatalog.Describe(doc, titleBlockId)}\n" +
                (ok ? "VERIFY PASS" : "VERIFY ISSUES:\n• " + string.Join("\n• ", issues));

            WriteSmoke(ok, body);
            PlatformLog.Logger.Information("GMP smoke finished ok={Ok}", ok);
        }
        catch (Exception ex)
        {
            PlatformLog.Logger.Error(ex, "GMP smoke crashed");
            WriteSmoke(false, ex.ToString());
        }
        finally
        {
            WriteDoneFlag();
        }
    }

    private static void WriteSmoke(bool ok, string body)
    {
        try
        {
            PlatformPaths.EnsureDirectories();
            var path = Path.Combine(PlatformPaths.Reports, "smoke-verify.txt");
            File.WriteAllText(path, $"{(ok ? "PASS" : "FAIL")}\n{DateTimeOffset.Now:o}\n\n{body}");
        }
        catch
        {
            // ignored
        }
    }

    private static void WriteDoneFlag()
    {
        try
        {
            PlatformPaths.EnsureDirectories();
            File.WriteAllText(
                Path.Combine(PlatformPaths.Reports, "smoke-done.flag"),
                DateTimeOffset.Now.ToString("o"));
        }
        catch
        {
            // ignored
        }
    }
}
