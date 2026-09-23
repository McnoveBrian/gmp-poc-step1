using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using GmpPlatform.Core.Logging;
using GmpPlatform.Core.Planning;
using GmpPlatform.Core.Reports;
using GmpPlatform.Core.Settings;
using GmpPlatform.Core.Tickets;
using GmpPlatform.Revit.ShopTickets;
using GmpPlatform.Revit.UI;

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
            TicketDialogs.Problem("Select a wall panel first", error);
            return;
        }

        var context = WallPanelReader.ReadContext(panel);
        var snapshot = WallPanelReader.Read(panel);
        var plan = WallShopTicketPlanner.CreatePlan(snapshot);
        var settings = SettingsStore.Load();
        var titleBlockId = TitleBlockCatalog.ResolveForShopTicket(doc, settings, out var titleBlockNote);

        var proceed = TicketDialogs.Confirm(
            new TicketDialogs.PieceSummary
            {
                Mark = snapshot.Mark,
                Family = snapshot.FamilyName,
                Type = snapshot.TypeName,
                LengthIn = TicketUnits.Inches(snapshot.LengthFt),
                HeightIn = TicketUnits.Inches(snapshot.HeightFt),
                ThicknessIn = TicketUnits.Inches(snapshot.ThicknessFt),
                PartTypes = snapshot.Parts.Count,
                TitleBlock = titleBlockId == Autodesk.Revit.DB.ElementId.InvalidElementId
                    ? ""
                    : TitleBlockCatalog.Describe(doc, titleBlockId),
                TitleBlockNote = titleBlockNote,
                Notes = plan.Warnings
            },
            what: "Revit shop-ticket sheets",
            goLabel: "Generate sheets",
            goDetail: "Creates the TIF, BIF and Sections sheets in this model and opens them for review.");

        if (!proceed)
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

            // The hand-off the AutoCAD side consumes. Written every run so the drawing
            // engine can be re-run, diffed and tested without opening Revit again.
            var ticket = PieceTicketExporter.Build(panel, context);
            var ticketPath = TicketJson.Write(ticket, reportFolder);
            PlatformLog.Logger.Information("Piece ticket written to {Path}", ticketPath);

            // Open every new sheet as a tab so the user can review immediately.
            SheetNavigator.OpenSheets(uidoc, result.CreatedSheetIds);

            TicketDialogs.Done(
                headline: $"Shop-ticket sheets created for {snapshot.Mark}",
                items: result.CreatedSheetNames,
                folder: reportFolder,
                notes: plan.Warnings);
        }
        catch (Exception ex)
        {
            PlatformLog.Logger.Error(ex, "Shop ticket generation failed");
            TicketDialogs.Problem(
                $"The sheets for {snapshot.Mark} could not be created",
                "If the error came after the sheets were made, they are still in the model — " +
                "check the Project Browser. The details below are also in the log.",
                ex.ToString());
        }
    }
}
