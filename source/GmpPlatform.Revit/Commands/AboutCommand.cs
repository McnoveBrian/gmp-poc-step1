using System.Diagnostics;
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
        TaskDialog.Show(
            "GMP Production Platform",
            "GMP Production Platform — Demo 0.1.0\n\n" +
            "Revit → shop ticket sheets → DWG export (demo framework)\n" +
            "Target: Revit 2025 · C# / Revit API\n\n" +
            $"Data root:\n{PlatformPaths.Root}\n\n" +
            "Select a wall panel, then use Shop Tickets → Wall Ticket.\n" +
            "Use Shop Tickets → Settings to choose the sheet title block first.");
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
