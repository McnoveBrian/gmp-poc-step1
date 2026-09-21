using Autodesk.Revit.DB;
using GmpPlatform.Core.Settings;
using GmpPlatform.Revit.ShopTickets;
using WinForms = System.Windows.Forms;

namespace GmpPlatform.Revit.UI;

/// <summary>Shop-ticket settings: title block, scale, clearances, apply-to-existing sheets.</summary>
internal sealed class SettingsForm : WinForms.Form
{
    private readonly Document _doc;
    private readonly WinForms.ComboBox _titleBlocks;
    private readonly WinForms.ComboBox _viewScale;
    private readonly WinForms.NumericUpDown _noteClearance;
    private readonly WinForms.NumericUpDown _sectionGap;
    private readonly WinForms.CheckBox _applyExisting;
    private readonly WinForms.Label _pathLabel;

    public bool ApplyToExistingSheets { get; private set; }

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
        ClientSize = new System.Drawing.Size(540, 360);
        Font = new System.Drawing.Font("Segoe UI", 9f);

        var titleLabel = new WinForms.Label
        {
            Text = "Title block for shop-ticket sheets",
            AutoSize = true,
            Location = new System.Drawing.Point(16, 16)
        };

        _titleBlocks = new WinForms.ComboBox
        {
            DropDownStyle = WinForms.ComboBoxStyle.DropDownList,
            Location = new System.Drawing.Point(16, 40),
            Width = 500,
            DisplayMember = nameof(TitleBlockOption.DisplayName)
        };
        _titleBlocks.Items.Add(new TitleBlockOption(
            ElementId.InvalidElementId,
            string.Empty,
            "(none — blank sheet)"));
        foreach (var opt in options)
            _titleBlocks.Items.Add(opt);

        _titleBlocks.SelectedIndex = ResolveTitleBlockIndex(settings, options);

        var scaleLabel = new WinForms.Label
        {
            Text = "View scale (fit sheet)",
            AutoSize = true,
            Location = new System.Drawing.Point(16, 84)
        };
        _viewScale = new WinForms.ComboBox
        {
            DropDownStyle = WinForms.ComboBoxStyle.DropDownList,
            Location = new System.Drawing.Point(280, 80),
            Width = 236
        };
        _viewScale.Items.AddRange(new object[]
        {
            "Auto-fit to sheet",
            "1/8\" = 1'-0\" (1:96)",
            "3/16\" = 1'-0\" (1:64)",
            "1/4\" = 1'-0\" (1:48)",
            "3/8\" = 1'-0\" (1:32)",
            "1/2\" = 1'-0\" (1:24)",
            "3/4\" = 1'-0\" (1:16)",
            "1\" = 1'-0\" (1:12)",
            "1 1/2\" = 1'-0\" (1:8)",
            "3\" = 1'-0\" (1:4)"
        });
        _viewScale.SelectedIndex = ScaleToIndex(settings.ViewScale);

        var noteLabel = new WinForms.Label
        {
            Text = "Note / geometry clearance (ft)",
            AutoSize = true,
            Location = new System.Drawing.Point(16, 128)
        };
        _noteClearance = new WinForms.NumericUpDown
        {
            DecimalPlaces = 2,
            Minimum = 0.5m,
            Maximum = 6m,
            Increment = 0.25m,
            Value = Clamp((decimal)settings.NoteClearanceFt, 0.5m, 6m),
            Location = new System.Drawing.Point(280, 124),
            Width = 100
        };

        var gapLabel = new WinForms.Label
        {
            Text = "Section cell gap (ft)",
            AutoSize = true,
            Location = new System.Drawing.Point(16, 168)
        };
        _sectionGap = new WinForms.NumericUpDown
        {
            DecimalPlaces = 2,
            Minimum = 0.75m,
            Maximum = 8m,
            Increment = 0.25m,
            Value = Clamp((decimal)settings.SectionCellGapFt, 0.75m, 8m),
            Location = new System.Drawing.Point(280, 164),
            Width = 100
        };

        _applyExisting = new WinForms.CheckBox
        {
            Text = "Apply title block to all existing shop-ticket sheets now",
            AutoSize = true,
            Checked = settings.ApplyTitleBlockToExistingSheets,
            Location = new System.Drawing.Point(16, 208)
        };

        _pathLabel = new WinForms.Label
        {
            Text = $"Saved to:\n{SettingsStore.FilePath}",
            AutoSize = false,
            Location = new System.Drawing.Point(16, 244),
            Size = new System.Drawing.Size(500, 40)
        };

        var save = new WinForms.Button
        {
            Text = "Save",
            DialogResult = WinForms.DialogResult.OK,
            Location = new System.Drawing.Point(336, 310),
            Width = 88
        };
        save.Click += (_, _) => SaveSettings();

        var cancel = new WinForms.Button
        {
            Text = "Cancel",
            DialogResult = WinForms.DialogResult.Cancel,
            Location = new System.Drawing.Point(432, 310),
            Width = 88
        };

        AcceptButton = save;
        CancelButton = cancel;

        Controls.Add(titleLabel);
        Controls.Add(_titleBlocks);
        Controls.Add(scaleLabel);
        Controls.Add(_viewScale);
        Controls.Add(noteLabel);
        Controls.Add(_noteClearance);
        Controls.Add(gapLabel);
        Controls.Add(_sectionGap);
        Controls.Add(_applyExisting);
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
        settings.ViewScale = IndexToScale(_viewScale.SelectedIndex);
        settings.ApplyTitleBlockToExistingSheets = _applyExisting.Checked;
        ApplyToExistingSheets = _applyExisting.Checked;

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

    private static int ResolveTitleBlockIndex(PlatformSettings settings, IReadOnlyList<TitleBlockOption> options)
    {
        if (!string.IsNullOrWhiteSpace(settings.TitleBlockUniqueId))
        {
            for (var i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i].UniqueId, settings.TitleBlockUniqueId, StringComparison.OrdinalIgnoreCase))
                    return i + 1;
            }
        }

        if (!string.IsNullOrWhiteSpace(settings.TitleBlockFamilyAndType))
        {
            for (var i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i].DisplayName, settings.TitleBlockFamilyAndType, StringComparison.OrdinalIgnoreCase))
                    return i + 1;
            }
        }

        return 0;
    }

    private static int ScaleToIndex(int scale) => scale switch
    {
        96 => 1,
        64 => 2,
        48 => 3,
        32 => 4,
        24 => 5,
        16 => 6,
        12 => 7,
        8 => 8,
        4 => 9,
        _ => 0
    };

    private static int IndexToScale(int index) => index switch
    {
        1 => 96,
        2 => 64,
        3 => 48,
        4 => 32,
        5 => 24,
        6 => 16,
        7 => 12,
        8 => 8,
        9 => 4,
        _ => 0
    };

    private static decimal Clamp(decimal value, decimal min, decimal max) =>
        value < min ? min : value > max ? max : value;
}
