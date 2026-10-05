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

        var settings = SettingsStore.Load();
        if (SettingsStore.LastLoadError != null)
            PlatformLog.Logger.Warning("Settings file {Path} could not be read; defaults in use. {Error}",
                SettingsStore.FilePath, SettingsStore.LastLoadError);
        else
            PlatformLog.Logger.Information("Settings read from {Path}. Seed drawing: {Seed}",
                SettingsStore.FilePath, settings.SeedDwgPath ?? "(not set)");

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
                ShopTicketSmoke.WriteResultFile(false, "Smoke skipped: no project document.");
                WriteDoneFlag();
                return;
            }

            PlatformLog.Logger.Information("GMP smoke starting on {Title}", doc.Title);

            var settings = SettingsStore.Load();
            var titleBlockId = ShopTicketSmoke.ResolveTitleBlock(doc, settings);
            var panel = ShopTicketSmoke.FindAnyPanel(doc);

            if (panel == null)
            {
                ShopTicketSmoke.WriteResultFile(false, "Smoke failed: no Structural Framing / Wall element found.");
                WriteDoneFlag();
                return;
            }

            var result = ShopTicketSmoke.Run(doc, panel, settings, titleBlockId);
            ShopTicketSmoke.WriteResultFile(result.Ok, result.Body);
            PlatformLog.Logger.Information("GMP smoke finished ok={Ok}", result.Ok);
        }
        catch (Exception ex)
        {
            PlatformLog.Logger.Error(ex, "GMP smoke crashed");
            ShopTicketSmoke.WriteResultFile(false, ex.ToString());
        }
        finally
        {
            WriteDoneFlag();
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
