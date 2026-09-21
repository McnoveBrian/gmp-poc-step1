using Nice3point.Revit.Toolkit.External;
using GmpPlatform.Core.Logging;
using GmpPlatform.Revit.Ribbon;

namespace GmpPlatform.Revit;

[UsedImplicitly]
public class Application : ExternalApplication
{
    public override void OnStartup()
    {
        var version = Application.ControlledApplication.VersionNumber;
        PlatformLog.Initialize(version);
        PlatformLog.Logger.Information("GMP Production Platform starting. Revit {Version}", version);
        RibbonBuilder.Create(Application);
    }

    public override void OnShutdown()
    {
        PlatformLog.Logger.Information("GMP Production Platform shutting down");
        PlatformLog.Shutdown();
    }
}
