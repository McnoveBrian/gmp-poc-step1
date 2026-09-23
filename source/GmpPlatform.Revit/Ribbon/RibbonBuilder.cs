using Autodesk.Revit.UI;
using Nice3point.Revit.Extensions;
using GmpPlatform.Core.Settings;
using GmpPlatform.Revit.Commands;

namespace GmpPlatform.Revit.Ribbon;

/// <summary>
/// Builds the GMP Platform ribbon tab.
/// <para>
/// The default ribbon is the one a client sees: generate the ticket, set it up, get help.
/// Development tools — the retired Revit-sheet route and the buttons that exist only to serve
/// it — appear only when <see cref="PlatformSettings.ShowDeveloperTools"/> is on. A demo ribbon
/// should not offer a second, weaker way to do the main job; a visitor who clicks it sees the
/// weaker output and remembers that.
/// </para>
/// </summary>
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

        var settings = SettingsStore.Load();
        var shop = application.CreatePanel("Shop Tickets", TabName);

        // The product. First, and with the ticket icon, because it is the one thing the tab is for.
        var drawing = shop.AddPushButton<GenerateTicketDrawingCommand>("Ticket Drawing");
        drawing.SetImage($"{IconRoot}/WallTicket16-Light.png");
        drawing.SetLargeImage($"{IconRoot}/WallTicket32-Light.png");
        drawing.SetToolTip("Generate the shop ticket for the selected panel");
        drawing.SetLongDescription(
            "Reads the selected panel from the model and draws its shop ticket into a copy of the " +
            "client's own DWG through AutoCAD, then plots the sheets to PDF. The ticket uses the " +
            "client's layers, dimension styles, blocks and title blocks. Anything computed rather " +
            "than read from the model is marked on the drawing for review. " +
            "Set SeedDwgPath in settings before first use.");

        var settingsButton = shop.AddPushButton<SettingsCommand>("Settings");
        settingsButton.SetImage($"{IconRoot}/Settings16-Light.png");
        settingsButton.SetLargeImage($"{IconRoot}/Settings32-Light.png");
        settingsButton.SetToolTip("Choose the title block and layout options");
        settingsButton.SetLongDescription(
            "Select which title block is placed on generated sheets, and adjust note and section " +
            "clearances so drafting content does not overlap.");

        var help = application.CreatePanel("Help", TabName);

        var about = help.AddPushButton<AboutCommand>("About");
        about.SetImage($"{IconRoot}/About16-Light.png");
        about.SetLargeImage($"{IconRoot}/About32-Light.png");
        about.SetToolTip("Version and paths");

        var openReports = help.AddPushButton<OpenReportsCommand>("Reports");
        openReports.SetImage($"{IconRoot}/Reports16-Light.png");
        openReports.SetLargeImage($"{IconRoot}/Reports32-Light.png");
        openReports.SetToolTip("Open the run reports folder");

        if (settings.ShowDeveloperTools) AddDeveloperTools(application);
    }

    /// <summary>
    /// The retired Revit-sheet route and its helpers, on a panel of their own so it is obvious
    /// they are not part of the product.
    /// </summary>
    private static void AddDeveloperTools(UIControlledApplication application)
    {
        var dev = application.CreatePanel("Developer", TabName);

        var wallTicket = dev.AddPushButton<GenerateWallShopTicketCommand>("Wall Ticket");
        wallTicket.SetImage($"{IconRoot}/WallTicket16-Light.png");
        wallTicket.SetLargeImage($"{IconRoot}/WallTicket32-Light.png");
        wallTicket.SetToolTip("Retired: builds placeholder shop-ticket sheets inside Revit");
        wallTicket.SetLongDescription(
            "The earlier route, kept for development. It creates TIF, BIF and Sections sheets in " +
            "this model from drafting views and placeholder notes. Use Ticket Drawing for the real " +
            "ticket.");

        var export = dev.AddPushButton<ExportShopTicketDwgCommand>("Export DWG");
        export.SetImage($"{IconRoot}/ExportDwg16-Light.png");
        export.SetLargeImage($"{IconRoot}/ExportDwg32-Light.png");
        export.SetToolTip("Export Wall Ticket sheets to DWG");

        var verify = dev.AddPushButton<SmokeVerifyCommand>("Verify");
        verify.SetImage($"{IconRoot}/About16-Light.png");
        verify.SetLargeImage($"{IconRoot}/About32-Light.png");
        verify.SetToolTip("Generate Wall Ticket sheets and check title blocks and scales");
    }
}
