using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using GmpPlatform.Core;
using GmpPlatform.Core.Interop;
using GmpPlatform.Core.Logging;
using GmpPlatform.Core.Settings;
using GmpPlatform.Core.Tickets;
using GmpPlatform.Revit.ShopTickets;
using GmpPlatform.Revit.UI;

namespace GmpPlatform.Revit.Commands;

/// <summary>
/// Select a panel, get the finished AutoCAD ticket: reads the piece, writes the hand-off,
/// draws it into the client's drawing through headless AutoCAD, and plots the sheets.
/// <para>
/// This is the whole chain behind one button. Previously it took a Revit command, a set of
/// environment variables and two console invocations by hand, which is fine for development
/// and useless for a demonstration.
/// </para>
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class GenerateTicketDrawingCommand : ExternalCommand
{
    public override void Execute()
    {
        var uidoc = Application.ActiveUIDocument
                    ?? throw new InvalidOperationException("No active document.");

        if (!WallPanelReader.TryGetSelectedPanel(uidoc, out var panel, out var error))
        {
            TicketDialogs.Problem("Select a wall panel first", error);
            return;
        }

        var settings = SettingsStore.Load();
        var pluginDll = FindDrawingEngine(settings);

        var seed = settings.SeedDwgPath;
        if (string.IsNullOrWhiteSpace(seed) || !File.Exists(seed))
        {
            var detail = $"Settings file:\n{SettingsStore.FilePath}";
            if (SettingsStore.LastLoadError != null)
                detail += $"\n\nThe file could not be read, so defaults were used:\n{SettingsStore.LastLoadError}";

            TicketDialogs.Problem(
                "The client's template drawing is not set",
                "The ticket is drawn into a copy of the client's approved DWG, so it inherits their " +
                "layers, styles, blocks and title blocks. Set SeedDwgPath in the settings file to " +
                "that drawing, then try again.",
                detail);
            return;
        }

        if (!File.Exists(pluginDll))
        {
            TicketDialogs.Problem(
                "The drawing engine is not installed",
                "GmpPlatform.Acad.dll should sit next to the add-in. Rebuild the whole solution " +
                "(not just the Revit project) so it is copied there.",
                $"Looked for:\n{pluginDll}");
            return;
        }

        var context = WallPanelReader.ReadContext(panel);
        var ticket = PieceTicketExporter.Build(panel, context);

        var proceed = TicketDialogs.Confirm(
            new TicketDialogs.PieceSummary
            {
                Mark = ticket.Identity.Mark,
                Family = ticket.Identity.FamilyName,
                Type = ticket.Identity.TypeName,
                LengthIn = ticket.Dimensions.LengthIn,
                HeightIn = ticket.Dimensions.HeightIn,
                ThicknessIn = ticket.Dimensions.ThicknessIn,
                PartTypes = ticket.Parts.Count,
                TitleBlock = $"from {Path.GetFileName(seed)}",
                Notes = ticket.Warnings
            },
            what: "the AutoCAD shop ticket",
            goLabel: "Generate ticket drawing",
            goDetail: "Draws the ticket into a copy of the client's DWG and plots three PDF sheets. " +
                      "AutoCAD runs in the background; allow about a minute.");

        if (!proceed) return;

        PlatformPaths.EnsureDirectories();
        var outputFolder = Path.Combine(
            PlatformPaths.Exports,
            $"{DateTime.Now:yyyyMMdd-HHmmss}-{Sanitize(ticket.Identity.Mark)}");

        var ticketPath = TicketJson.Write(ticket, outputFolder);
        PlatformLog.Logger.Information("Piece ticket written to {Path}", ticketPath);

        var result = AcadRunner.Run(
            ticketPath,
            // Checked above; .NET Framework (Revit 2024) lacks the annotation that lets the
            // compiler see that through IsNullOrWhiteSpace.
            seed!,
            pluginDll,
            outputFolder,
            settings.AutoCadConsolePath,
            settings.FontMapPath,
            settings.PlotAfterDrawing);

        PlatformLog.Logger.Information("AutoCAD run success={Success}: {Message}", result.Success, result.Message);

        if (!result.Success)
        {
            TicketDialogs.Problem(
                $"The ticket for {ticket.Identity.Mark} did not complete",
                result.Message,
                $"Output folder:\n{outputFolder}");
            return;
        }

        var items = new List<string> { Path.GetFileName(result.OutputDwg) };
        items.AddRange(result.PdfFiles.Select(Path.GetFileName)!);

        TicketDialogs.Done(
            headline: $"Shop ticket created for {ticket.Identity.Mark}",
            items: items!,
            folder: outputFolder,
            firstFile: result.PdfFiles.FirstOrDefault(),
            firstFileLabel: "Open sheet 1",
            notes: ticket.Warnings);
    }

    /// <summary>
    /// The drawing engine DLL: beside the add-in where the build copies it, or wherever
    /// settings point when running the two projects from separate output folders.
    /// </summary>
    private static string FindDrawingEngine(PlatformSettings settings)
    {
        var beside = Path.Combine(
            Path.GetDirectoryName(typeof(GenerateTicketDrawingCommand).Assembly.Location)!,
            "GmpPlatform.Acad.dll");

        if (File.Exists(beside)) return beside;
        if (!string.IsNullOrWhiteSpace(settings.AcadPluginPath) && File.Exists(settings.AcadPluginPath))
            return settings.AcadPluginPath!;

        return beside; // reported as missing by the caller
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "piece" : name;
    }
}
