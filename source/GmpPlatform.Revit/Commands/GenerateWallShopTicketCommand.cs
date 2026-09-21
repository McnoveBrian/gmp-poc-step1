using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using GmpPlatform.Core.Logging;
using GmpPlatform.Core.Planning;
using GmpPlatform.Core.Reports;
using GmpPlatform.Core.Settings;
using GmpPlatform.Revit.ShopTickets;

namespace GmpPlatform.Revit.Commands;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class GenerateWallShopTicketCommand : ExternalCommand
{
    public override void Execute()
    {
        var uidoc = Application.ActiveUIDocument
                     ?? throw new InvalidOperationException("No active document.");
        var doc = uidoc.Document;

        if (!WallPanelReader.TryGetSelectedPanel(uidoc, out var panel, out var error))
        {
            TaskDialog.Show("GMP Platform", error);
            return;
        }

        var snapshot = WallPanelReader.Read(panel);
        var plan = WallShopTicketPlanner.CreatePlan(snapshot);
        var settings = SettingsStore.Load();
        var titleBlockId = TitleBlockCatalog.ResolvePreferredId(doc, settings);
        var titleBlockLabel = TitleBlockCatalog.Describe(doc, titleBlockId);

        var confirm = new TaskDialog("GMP Platform — Generate Wall Shop Ticket")
        {
            MainInstruction = $"Generate shop ticket for mark '{snapshot.Mark}'?",
            MainContent =
                $"Family: {snapshot.FamilyName}\n" +
                $"Type: {snapshot.TypeName}\n" +
                $"Size (ft): L={snapshot.LengthFt:0.###}  H={snapshot.HeightFt:0.###}  T={snapshot.ThicknessFt:0.###}\n" +
                $"Title block: {titleBlockLabel}\n\n" +
                $"Creates 3 sheets (TIF / BIF / Sections+BOM), opens them for review, " +
                $"and uses auto-sized text notes.\n\n" +
                (titleBlockId == Autodesk.Revit.DB.ElementId.InvalidElementId
                    ? "Tip: use Shop Tickets → Settings to pick a title block before generating.\n\n"
                    : "") +
                (plan.Warnings.Count > 0
                    ? "Notes:\n• " + string.Join("\n• ", plan.Warnings)
                    : "No warnings."),
            CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No
        };

        if (confirm.Show() != TaskDialogResult.Yes)
        {
            PlatformLog.Logger.Information("Shop ticket generation cancelled by user");
            return;
        }

        try
        {
            var generator = new WallShopTicketGenerator(doc, panel, plan, settings);
            var result = generator.Generate();
            var reportFolder = RunReportWriter.Write(plan, result);
            result.ReportPath = reportFolder;

            // Open every new sheet as a tab so the user can review immediately.
            SheetNavigator.OpenSheets(uidoc, result.CreatedSheetIds);

            TaskDialog.Show(
                "GMP Platform — Done",
                $"{result.Message}\n\n" +
                $"Sheets (opened for review):\n• {string.Join("\n• ", result.CreatedSheetNames)}\n\n" +
                $"Report:\n{reportFolder}");
        }
        catch (Exception ex)
        {
            PlatformLog.Logger.Error(ex, "Shop ticket generation failed");
            TaskDialog.Show("GMP Platform — Error", ex.Message);
        }
    }
}
