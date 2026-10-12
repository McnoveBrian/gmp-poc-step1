using System.Diagnostics;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using GmpPlatform.Core;

namespace GmpPlatform.Revit.Commands;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class AboutCommand : ExternalCommand
{
    public override void Execute()
    {
        PlatformPaths.EnsureDirectories();

        var asm = typeof(AboutCommand).Assembly;
        var informational = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        var version = string.IsNullOrWhiteSpace(informational)
            ? asm.GetName().Version?.ToString() ?? "?"
            : informational.Split('+')[0];
        var revitYear = UiApplication.Application.VersionNumber;

        TaskDialog.Show(
            "GMP Production Platform",
            $"GMP Production Platform {version}\n\n" +
            "Revit → AutoCAD piece ticket (client seed DWG) → PDF\n" +
            $"Running in Revit {revitYear}\n\n" +
            $"Data root:\n{PlatformPaths.Root}\n\n" +
            "Select a wall panel, then use Shop Tickets → Ticket Drawing.\n" +
            "Use Shop Tickets → Settings to set the seed DWG and font map.");
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class OpenReportsCommand : ExternalCommand
{
    public override void Execute()
    {
        PlatformPaths.EnsureDirectories();
        Process.Start(new ProcessStartInfo
        {
            FileName = PlatformPaths.Reports,
            UseShellExecute = true
        });
    }
}
