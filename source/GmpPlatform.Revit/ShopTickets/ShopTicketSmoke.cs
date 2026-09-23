using System.Text;
using Autodesk.Revit.DB;
using GmpPlatform.Core;
using GmpPlatform.Core.Logging;
using GmpPlatform.Core.Planning;
using GmpPlatform.Core.Reports;
using GmpPlatform.Core.Settings;

namespace GmpPlatform.Revit.ShopTickets;

/// <summary>
/// Generates a shop ticket and checks the result: sheets created, title blocks present and of
/// the configured type, viewports present with a real scale.
/// <para>
/// This exists in one place because it used to exist in two — about 160 near-identical lines in
/// <c>Application</c>'s startup smoke and in <c>SmokeVerifyCommand</c>. They had already begun
/// to drift: one looked sheets up through <see cref="TitleBlockService.FindShopTicketSheets"/>
/// and checked view scales, the other did neither. A self-check that disagrees with itself
/// depending on how it was invoked is worse than no self-check.
/// </para>
/// </summary>
internal static class ShopTicketSmoke
{
    internal sealed class Result
    {
        public bool Ok { get; init; }
        public string Body { get; init; } = "";
        public string ReportFolder { get; init; } = "";
    }

    /// <summary>Any element that could plausibly be a wall panel, preferring a WALL family.</summary>
    public static Element? FindAnyPanel(Document doc)
    {
        bool IsPanelCategory(Element el)
        {
            var c = (int)(el.Category?.Id.Value ?? 0);
            return c == (int)BuiltInCategory.OST_StructuralFraming ||
                   c == (int)BuiltInCategory.OST_Walls;
        }

        return new FilteredElementCollector(doc)
                   .WhereElementIsNotElementType()
                   .FirstOrDefault(el =>
                   {
                       if (!IsPanelCategory(el)) return false;
                       var type = doc.GetElement(el.GetTypeId()) as ElementType;
                       var family = type is FamilySymbol fs ? fs.FamilyName : type?.Name ?? "";
                       return family.IndexOf("WALL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              el.Category?.Name.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) >= 0;
                   })
               ?? new FilteredElementCollector(doc)
                   .WhereElementIsNotElementType()
                   .FirstOrDefault(IsPanelCategory);
    }

    /// <summary>
    /// Resolves the title block to use, falling back to the first available and remembering
    /// the choice so later runs are consistent.
    /// </summary>
    /// <summary>
    /// The shop-ticket title block, through the one shared resolver.
    /// <para>
    /// This used to take <c>List().First()</c> and save it to settings. On this model the
    /// first entry is the project cover page, so every ticket came out on front matter — and
    /// because the pick was saved, it stayed that way. Nothing is saved here any more: a guess
    /// should not quietly become a preference.
    /// </para>
    /// </summary>
    public static ElementId ResolveTitleBlock(Document doc, PlatformSettings settings) =>
        TitleBlockCatalog.ResolveForShopTicket(doc, settings, out _);

    public static Result Run(Document doc, Element panel, PlatformSettings settings, ElementId titleBlockId)
    {
        var snapshot = WallPanelReader.Read(panel);
        var plan = WallShopTicketPlanner.CreatePlan(snapshot);
        var generator = new WallShopTicketGenerator(doc, panel, plan, settings);
        var result = generator.Generate();
        var reportFolder = RunReportWriter.Write(plan, result);

        var sheets = FindCreatedSheets(doc, result.CreatedSheetNames);
        var issues = new List<string>();

        if (sheets.Count < 3)
            issues.Add($"Expected 3 sheets, found {sheets.Count}");

        foreach (var sheet in sheets)
        {
            var titleBlocks = TitleBlockService.GetTitleBlockInstances(doc, sheet).ToList();
            if (titleBlockId != ElementId.InvalidElementId && titleBlocks.Count == 0)
                issues.Add($"{sheet.SheetNumber}: missing title block");
            else if (titleBlockId != ElementId.InvalidElementId &&
                     titleBlocks.Any(t => t.GetTypeId() != titleBlockId))
                issues.Add($"{sheet.SheetNumber}: title block type mismatch");

            var viewports = sheet.GetAllViewports();
            if (viewports.Count == 0)
            {
                issues.Add($"{sheet.SheetNumber}: no viewports");
                continue;
            }

            foreach (var id in viewports)
            {
                if (doc.GetElement(id) is not Viewport viewport) continue;
                if (doc.GetElement(viewport.ViewId) is not View view) continue;
                if (view.Scale <= 0) issues.Add($"{sheet.SheetNumber}/{view.Name}: invalid scale");
            }
        }

        // Re-apply on just the sheets from this run, rather than scanning the whole model.
        if (titleBlockId != ElementId.InvalidElementId && sheets.Count > 0)
        {
            using var tx = new Transaction(doc, "GMP smoke - reapply title blocks");
            tx.Start();
            var applied = TitleBlockService.ApplyToSheets(doc, sheets, titleBlockId);
            tx.Commit();
            if (applied < sheets.Count) issues.Add($"Reapply touched {applied}/{sheets.Count} sheets");
        }

        var ok = issues.Count == 0 && result.Success;

        var body = new StringBuilder()
            .AppendLine(result.Message)
            .AppendLine($"Sheets checked: {sheets.Count} — {string.Join(" | ", result.CreatedSheetNames)}")
            .AppendLine($"Title block: {TitleBlockCatalog.Describe(doc, titleBlockId)}")
            .AppendLine($"Report: {reportFolder}");

        if (ok) body.AppendLine("VERIFY PASS: title blocks present on sheets; view scales set.");
        else
        {
            body.AppendLine("VERIFY ISSUES:");
            foreach (var issue in issues) body.AppendLine("• " + issue);
        }

        return new Result { Ok = ok, Body = body.ToString(), ReportFolder = reportFolder };
    }

    private static List<ViewSheet> FindCreatedSheets(Document doc, IReadOnlyCollection<string> createdNames)
    {
        var sheets = TitleBlockService.FindShopTicketSheets(doc)
            .Where(s => createdNames.Any(n => n.Contains(s.SheetNumber, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (sheets.Count >= 3) return sheets;

        // Fall back to matching by sheet number across all sheets: FindShopTicketSheets
        // recognises our sheets by name, which is fragile enough not to rely on alone.
        return new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>()
            .Where(s => createdNames.Any(n => n.Contains(s.SheetNumber, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>Writes the smoke result where the harness and a human both look for it.</summary>
    public static void WriteResultFile(bool ok, string body)
    {
        try
        {
            PlatformPaths.EnsureDirectories();
            File.WriteAllText(
                Path.Combine(PlatformPaths.Reports, "smoke-verify.txt"),
                $"{(ok ? "PASS" : "FAIL")}\n{DateTimeOffset.Now:o}\n\n{body}");
        }
        catch (Exception ex)
        {
            PlatformLog.Logger.Warning(ex, "Could not write the smoke result file");
        }
    }
}
