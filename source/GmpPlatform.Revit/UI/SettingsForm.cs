using Autodesk.Revit.DB;
using GmpPlatform.Core.Settings;
using GmpPlatform.Revit.ShopTickets;
using WinForms = System.Windows.Forms;

namespace GmpPlatform.Revit.UI;

/// <summary>Simple picker for the shop-ticket title block and layout clearances.</summary>
internal sealed class SettingsForm : WinForms.Form
{
    private readonly Document _doc;
    private readonly WinForms.ComboBox _titleBlocks;
    private readonly WinForms.NumericUpDown _noteClearance;
    private readonly WinForms.NumericUpDown _sectionGap;
    private readonly WinForms.Label _pathLabel;

    public SettingsForm(Document doc)
    {
        _doc = doc;
        var settings = SettingsStore.Load();
        var options = TitleBlockCatalog.List(doc);

        Text = "GMP Platform — Settings";
        FormBorderStyle = WinForms.FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = WinForms.FormStartPosition.CenterScreen;
        ClientSize = new System.Drawing.Size(520, 280);
        Font = new System.Drawing.Font("Segoe UI", 9f);

        var titleLabel = new WinForms.Label
        {
            Text = "Title block for generated shop-ticket sheets",
            AutoSize = true,
            Location = new System.Drawing.Point(16, 16)
        };

        _titleBlocks = new WinForms.ComboBox
        {
            DropDownStyle = WinForms.ComboBoxStyle.DropDownList,
            Location = new System.Drawing.Point(16, 40),
            Width = 480,
            DisplayMember = nameof(TitleBlockOption.DisplayName)
        };
        _titleBlocks.Items.Add(new TitleBlockOption(
            ElementId.InvalidElementId,
            string.Empty,
            "(none — blank sheet)"));
        foreach (var opt in options)
            _titleBlocks.Items.Add(opt);

        var selectedIndex = 0;
        if (!string.IsNullOrWhiteSpace(settings.TitleBlockUniqueId))
        {
            for (var i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i].UniqueId, settings.TitleBlockUniqueId, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i + 1;
                    break;
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(settings.TitleBlockFamilyAndType))
        {
            for (var i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i].DisplayName, settings.TitleBlockFamilyAndType, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i + 1;
                    break;
                }
            }
        }

        _titleBlocks.SelectedIndex = Math.Min(selectedIndex, _titleBlocks.Items.Count - 1);

        var noteLabel = new WinForms.Label
        {
            Text = "Note / geometry clearance (ft)",
            AutoSize = true,
            Location = new System.Drawing.Point(16, 88)
        };
        _noteClearance = new WinForms.NumericUpDown
        {
            DecimalPlaces = 2,
            Minimum = 0.25m,
            Maximum = 5m,
            Increment = 0.25m,
            Value = Clamp((decimal)settings.NoteClearanceFt, 0.25m, 5m),
            Location = new System.Drawing.Point(280, 84),
            Width = 100
        };

        var gapLabel = new WinForms.Label
        {
            Text = "Section cell gap (ft)",
            AutoSize = true,
            Location = new System.Drawing.Point(16, 128)
        };
        _sectionGap = new WinForms.NumericUpDown
        {
            DecimalPlaces = 2,
            Minimum = 0.5m,
            Maximum = 6m,
            Increment = 0.25m,
            Value = Clamp((decimal)settings.SectionCellGapFt, 0.5m, 6m),
            Location = new System.Drawing.Point(280, 124),
            Width = 100
        };

        _pathLabel = new WinForms.Label
        {
            Text = $"Saved to:\n{SettingsStore.FilePath}",
            AutoSize = false,
            Location = new System.Drawing.Point(16, 168),
            Size = new System.Drawing.Size(480, 40)
        };

        var save = new WinForms.Button
        {
            Text = "Save",
            DialogResult = WinForms.DialogResult.OK,
            Location = new System.Drawing.Point(316, 230),
            Width = 88
        };
        save.Click += (_, _) => SaveSettings();

        var cancel = new WinForms.Button
        {
            Text = "Cancel",
            DialogResult = WinForms.DialogResult.Cancel,
            Location = new System.Drawing.Point(412, 230),
            Width = 88
        };

        AcceptButton = save;
        CancelButton = cancel;

        Controls.Add(titleLabel);
        Controls.Add(_titleBlocks);
        Controls.Add(noteLabel);
        Controls.Add(_noteClearance);
        Controls.Add(gapLabel);
        Controls.Add(_sectionGap);
        Controls.Add(_pathLabel);
        Controls.Add(save);
        Controls.Add(cancel);

        if (options.Count == 0)
        {
            WinForms.MessageBox.Show(
                this,
                "This document has no title-block family types loaded.\n" +
                "Load a title-block family into the project, then reopen Settings.",
                "GMP Platform",
                WinForms.MessageBoxButtons.OK,
                WinForms.MessageBoxIcon.Information);
        }
    }

    private void SaveSettings()
    {
        var settings = SettingsStore.Load();
        settings.NoteClearanceFt = (double)_noteClearance.Value;
        settings.SectionCellGapFt = (double)_sectionGap.Value;

        if (_titleBlocks.SelectedItem is not TitleBlockOption opt ||
            opt.SymbolId == ElementId.InvalidElementId ||
            string.IsNullOrWhiteSpace(opt.UniqueId))
        {
            settings.TitleBlockUniqueId = null;
            settings.TitleBlockFamilyAndType = null;
        }
        else
        {
            settings.TitleBlockUniqueId = opt.UniqueId;
            settings.TitleBlockFamilyAndType = opt.DisplayName;
            if (_doc.GetElement(opt.SymbolId) is FamilySymbol { IsActive: false } symbol)
            {
                using var tx = new Transaction(_doc, "GMP — Activate title block");
                tx.Start();
                symbol.Activate();
                tx.Commit();
            }
        }

        SettingsStore.Save(settings);
    }

    private static decimal Clamp(decimal value, decimal min, decimal max) =>
        value < min ? min : value > max ? max : value;
}
