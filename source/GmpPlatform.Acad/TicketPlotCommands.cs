using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.PlottingServices;
using Autodesk.AutoCAD.Runtime;

namespace GmpPlatform.Acad;

/// <summary>
/// Plots the generated sheets to PDF using each layout's own page setup, so the output goes
/// through the same device and paper size the client's drawing already specifies.
/// </summary>
public class TicketPlotCommands
{
    [CommandMethod("GMPTICKETPLOT", CommandFlags.Session)]
    public void PlotSheets()
    {
        var logPath = Environment.GetEnvironmentVariable("GMP_DRAW_LOG")
                      ?? Path.Combine(Path.GetTempPath(), "gmp-ticket-plot.log");
        var log = new DrawLog(logPath, append: true);

        try
        {
            var outDir = Environment.GetEnvironmentVariable("GMP_PLOT_DIR")
                         ?? Path.GetDirectoryName(logPath)
                         ?? Path.GetTempPath();
            Directory.CreateDirectory(outDir);

            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var stem = Path.GetFileNameWithoutExtension(db.Filename);

            var previousBackgroundPlot = (short)Autodesk.AutoCAD.ApplicationServices.Application
                .GetSystemVariable("BACKGROUNDPLOT");
            Autodesk.AutoCAD.ApplicationServices.Application.SetSystemVariable("BACKGROUNDPLOT", 0);

            var layouts = new List<string>();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var dict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
                foreach (DBDictionaryEntry entry in dict)
                {
                    var layout = (Layout)tr.GetObject(entry.Value, OpenMode.ForRead);
                    if (!layout.ModelType) layouts.Add(layout.LayoutName);
                }

                tr.Commit();
            }

            // Layout switching needs an explicit document lock in a session command;
            // without it every plot fails with eSetFailed.
            using var docLock = doc.LockDocument();
            var plotted = 0;

            foreach (var layoutName in layouts.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    LayoutManager.Current.CurrentLayout = layoutName;
                    using var tr = db.TransactionManager.StartTransaction();
                    var layout = (Layout)tr.GetObject(LayoutManager.Current.GetLayoutId(layoutName), OpenMode.ForRead);

                    var settings = new PlotSettings(layout.ModelType);
                    settings.CopyFrom(layout);
                    ApplyDeviceAndMedia(settings, layout, log, layoutName);

                    var info = new PlotInfo { Layout = layout.ObjectId, OverrideSettings = settings };
                    new PlotInfoValidator { MediaMatchingPolicy = MatchingPolicy.MatchEnabled }.Validate(info);

                    var file = Path.Combine(outDir, $"{stem}_{layoutName.Replace(" ", "_")}.pdf");
                    using (var engine = PlotFactory.CreatePublishEngine())
                    {
                        engine.BeginPlot(null, null);
                        engine.BeginDocument(info, doc.Name, null, 1, true, file);
                        engine.BeginPage(new PlotPageInfo(), info, true, null);
                        engine.BeginGenerateGraphics(null);
                        engine.EndGenerateGraphics(null);
                        engine.EndPage(null);
                        engine.EndDocument(null);
                        engine.EndPlot(null);
                    }

                    log.Info($"plotted {layoutName} -> {Path.GetFileName(file)}");
                    plotted++;
                    tr.Commit();
                }
                catch (System.Exception ex)
                {
                    log.Warn($"could not plot {layoutName}: {ex.Message}");
                }
            }

            Autodesk.AutoCAD.ApplicationServices.Application.SetSystemVariable("BACKGROUNDPLOT", previousBackgroundPlot);
            log.Info($"plot done: {plotted} sheet(s)");
        }
        catch (System.Exception ex)
        {
            log.Error("plot failed: " + ex);
        }
    }

    /// <summary>
    /// Keeps the layout's own device and paper where they resolve, and falls back to the PDF
    /// printer otherwise. The plot style the drawing asks for may not be installed here — the
    /// client's FINAL PRINT.ctb is theirs — so a missing one is reported, not fatal.
    /// </summary>
    private static void ApplyDeviceAndMedia(PlotSettings settings, Layout layout, DrawLog log, string layoutName)
    {
        var validator = PlotSettingsValidator.Current;

        try
        {
            validator.RefreshLists(settings);

            var device = layout.PlotConfigurationName;
            var devices = validator.GetPlotDeviceList().Cast<string>().ToList();
            if (!devices.Contains(device))
                device = devices.FirstOrDefault(d => d.IndexOf("DWG To PDF", StringComparison.OrdinalIgnoreCase) >= 0)
                         ?? device;

            validator.SetPlotConfigurationName(settings, device, null);
            validator.RefreshLists(settings);

            var media = validator.GetCanonicalMediaNameList(settings).Cast<string>().ToList();
            var wanted = media.FirstOrDefault(m => m == layout.CanonicalMediaName)
                         ?? media.FirstOrDefault(m => m.IndexOf("17.00_x_11.00", StringComparison.OrdinalIgnoreCase) >= 0)
                         ?? media.FirstOrDefault(m => m.IndexOf("ANSI_B", StringComparison.OrdinalIgnoreCase) >= 0);
            if (wanted != null) validator.SetCanonicalMediaName(settings, wanted);

            var sheets = validator.GetPlotStyleSheetList().Cast<string>().ToList();
            var style = layout.CurrentStyleSheet;
            if (!sheets.Contains(style))
            {
                log.Warn($"{layoutName}: plot style '{style}' is not installed here; " +
                         "plotting with monochrome.ctb instead (line weights will differ).");
                style = sheets.FirstOrDefault(s => s.Equals("monochrome.ctb", StringComparison.OrdinalIgnoreCase))
                        ?? sheets.FirstOrDefault() ?? "";
            }

            if (style.Length > 0) validator.SetCurrentStyleSheet(settings, style);

            validator.SetPlotType(settings, Autodesk.AutoCAD.DatabaseServices.PlotType.Extents);
            validator.SetUseStandardScale(settings, true);
            validator.SetStdScaleType(settings, StdScaleType.ScaleToFit);
            validator.SetPlotCentered(settings, true);
        }
        catch (System.Exception ex)
        {
            log.Warn($"{layoutName}: page setup could not be adjusted ({ex.Message}); using the layout's own.");
        }
    }
}
