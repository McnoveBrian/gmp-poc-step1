using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using GmpPlatform.Core.Settings;
using GmpPlatform.Revit.ShopTickets;
using GmpPlatform.Revit.UI;
using WinForms = System.Windows.Forms;

namespace GmpPlatform.Revit.Commands;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class SettingsCommand : ExternalCommand
{
    public override void Execute()
    {
        var uidoc = Application.ActiveUIDocument;
        if (uidoc == null)
        {
            TaskDialog.Show("GMP Platform", "Open a Revit project before changing settings.");
            return;
        }

        var doc = uidoc.Document;
        using var form = new SettingsForm(doc);
        if (form.ShowDialog() != WinForms.DialogResult.OK)
            return;

        var settings = SettingsStore.Load();
        var resolved = TitleBlockCatalog.ResolvePreferredId(doc, settings);
        var applied = 0;

        if (form.ApplyToExistingSheets && resolved != ElementId.InvalidElementId)
        {
            using var tx = new Transaction(doc, "GMP — Apply title block to shop-ticket sheets");
            tx.Start();
            applied = TitleBlockService.ApplyToShopTicketSheets(doc, resolved);
            tx.Commit();
        }

        var scaleLabel = settings.ViewScale <= 0 ? "Auto-fit to sheet" : $"1:{settings.ViewScale}";
        TaskDialog.Show(
            "GMP Platform — Settings",
            "Settings saved.\n\n" +
            $"Title block: {TitleBlockCatalog.Describe(doc, resolved)}\n" +
            $"View scale: {scaleLabel}\n" +
            $"Note clearance: {settings.NoteClearanceFt:0.##} ft\n" +
            $"Section cell gap: {settings.SectionCellGapFt:0.##} ft\n" +
            $"Existing sheets updated: {applied}\n\n" +
            $"File:\n{SettingsStore.FilePath}");
    }
}
