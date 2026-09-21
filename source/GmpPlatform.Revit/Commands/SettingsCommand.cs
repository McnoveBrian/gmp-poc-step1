using Autodesk.Revit.Attributes;
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

        using var form = new SettingsForm(uidoc.Document);
        if (form.ShowDialog() != WinForms.DialogResult.OK)
            return;

        var settings = SettingsStore.Load();
        var resolved = TitleBlockCatalog.ResolvePreferredId(uidoc.Document, settings);
        TaskDialog.Show(
            "GMP Platform — Settings",
            "Settings saved.\n\n" +
            $"Title block: {TitleBlockCatalog.Describe(uidoc.Document, resolved)}\n" +
            $"Note clearance: {settings.NoteClearanceFt:0.##} ft\n" +
            $"Section cell gap: {settings.SectionCellGapFt:0.##} ft\n\n" +
            $"File:\n{SettingsStore.FilePath}");
    }
}
