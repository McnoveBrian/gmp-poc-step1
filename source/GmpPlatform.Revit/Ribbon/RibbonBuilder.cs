using Autodesk.Revit.UI;
using Nice3point.Revit.Extensions;
using GmpPlatform.Revit.Commands;

namespace GmpPlatform.Revit.Ribbon;

internal static class RibbonBuilder
{
    private const string TabName = "GMP Platform";
    private const string IconRoot = "/GmpPlatform.Revit;component/Resources/Icons";

    public static void Create(UIControlledApplication application)
    {
        try
        {
            application.CreateRibbonTab(TabName);
        }
        catch (Autodesk.Revit.Exceptions.ArgumentException)
        {
            // Tab already exists
        }

        var shop = application.CreatePanel("Shop Tickets", TabName);

        var generate = shop.AddPushButton<GenerateWallShopTicketCommand>("Wall Ticket");
        generate.SetImage($"{IconRoot}/WallTicket16-Light.png");
        generate.SetLargeImage($"{IconRoot}/WallTicket32-Light.png");
        generate.SetToolTip("Generate a demo 3-sheet shop ticket for the selected wall panel");
        generate.SetLongDescription(
            "Select a wall panel (Structural Framing or Wall), then run this command. " +
            "Creates TIF / BIF / Sections+BOM sheets with drafting layouts, sample Detail Lines, " +
            "and a section cut when possible. Writes a run report under ProgramData\\GMP\\Platform\\reports.");

        var export = shop.AddPushButton<ExportShopTicketDwgCommand>("Export DWG");
        export.SetImage($"{IconRoot}/ExportDwg16-Light.png");
        export.SetLargeImage($"{IconRoot}/ExportDwg32-Light.png");
        export.SetToolTip("Export selected shop-ticket sheets to DWG (demo)");
        export.SetLongDescription(
            "Exports the active sheet (or selected sheets) to DWG under ProgramData\\GMP\\Platform\\exports. " +
            "Full client-style standards bridge comes in a later iteration.");

        var settings = shop.AddPushButton<SettingsCommand>("Settings");
        settings.SetImage($"{IconRoot}/Settings16-Light.png");
        settings.SetLargeImage($"{IconRoot}/Settings32-Light.png");
        settings.SetToolTip("Choose the title block and layout clearances for shop-ticket sheets");
        settings.SetLongDescription(
            "Select which title-block family/type is placed on generated sheets, " +
            "and adjust note/section clearances so drafting content does not overlap.");

        var help = application.CreatePanel("Help", TabName);

        var about = help.AddPushButton<AboutCommand>("About");
        about.SetImage($"{IconRoot}/About16-Light.png");
        about.SetLargeImage($"{IconRoot}/About32-Light.png");
        about.SetToolTip("Version and paths");

        var openReports = help.AddPushButton<OpenReportsCommand>("Reports");
        openReports.SetImage($"{IconRoot}/Reports16-Light.png");
        openReports.SetLargeImage($"{IconRoot}/Reports32-Light.png");
        openReports.SetToolTip("Open the run reports folder");
    }
}
