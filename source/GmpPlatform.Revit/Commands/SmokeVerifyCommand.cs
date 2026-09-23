using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using GmpPlatform.Core.Logging;
using GmpPlatform.Core.Settings;
using GmpPlatform.Revit.ShopTickets;

namespace GmpPlatform.Revit.Commands;

/// <summary>
/// Quiet end-to-end verification: pick a panel, generate sheets, assert title blocks + scales.
/// Set env GMP_SMOKE_SILENT=1 to skip dialogs (for journal automation).
/// <para>
/// The checking itself lives in <see cref="ShopTicketSmoke"/>, shared with the startup smoke,
/// so both report the same verdict on the same model.
/// </para>
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class SmokeVerifyCommand : ExternalCommand
{
    public override void Execute()
    {
        var silent = string.Equals(
            Environment.GetEnvironmentVariable("GMP_SMOKE_SILENT"),
            "1",
            StringComparison.Ordinal);

        var uidoc = Application.ActiveUIDocument;
        if (uidoc == null)
        {
            Report(silent, false, "No active document.");
            return;
        }

        var doc = uidoc.Document;
        var settings = SettingsStore.Load();
        var titleBlockId = ShopTicketSmoke.ResolveTitleBlock(doc, settings);

        if (!WallPanelReader.TryGetSelectedPanel(uidoc, out var panel, out _))
        {
            panel = ShopTicketSmoke.FindAnyPanel(doc);
            if (panel == null)
            {
                Report(silent, false, "No wall panel selected and none found in the model.");
                return;
            }

            uidoc.Selection.SetElementIds(new List<ElementId> { panel.Id });
        }

        try
        {
            var result = ShopTicketSmoke.Run(doc, panel!, settings, titleBlockId);
            ShopTicketSmoke.WriteResultFile(result.Ok, result.Body);
            Report(silent, result.Ok, result.Body);
        }
        catch (Exception ex)
        {
            PlatformLog.Logger.Error(ex, "Smoke verify failed");
            ShopTicketSmoke.WriteResultFile(false, ex.ToString());
            Report(silent, false, ex.Message);
        }
    }

    private static void Report(bool silent, bool ok, string message)
    {
        if (silent)
        {
            PlatformLog.Logger.Information("Smoke verify silent result ok={Ok}: {Message}", ok, message);
            return;
        }

        TaskDialog.Show(ok ? "GMP Platform — Verify PASS" : "GMP Platform — Verify FAIL", message);
    }
}
