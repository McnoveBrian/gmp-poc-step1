namespace GmpPlatform.Core.Settings;

/// <summary>User-configurable options for shop-ticket generation.</summary>
public sealed class PlatformSettings
{
    /// <summary>Revit UniqueId of the preferred title-block FamilySymbol, when known.</summary>
    public string? TitleBlockUniqueId { get; set; }

    /// <summary>Display name "Family : Type" used when UniqueId is missing or stale.</summary>
    public string? TitleBlockFamilyAndType { get; set; }

    /// <summary>Clearance (ft, drafting space) between panel geometry and note columns.</summary>
    public double NoteClearanceFt { get; set; } = 1.5;

    /// <summary>Clearance (ft) between neighboring section cells on the sections sheet.</summary>
    public double SectionCellGapFt { get; set; } = 2.0;

    /// <summary>Fraction of sheet width reserved on the right for the title-block column (fallback).</summary>
    public double TitleBlockRightReserve { get; set; } = 0.30;

    /// <summary>
    /// Drafting/section view scale. 0 = auto-fit to the sheet printable area.
    /// Common values: 96 (1/8"), 48 (1/4"), 24 (1/2"), 12 (1").
    /// </summary>
    public int ViewScale { get; set; } = 0;

    /// <summary>When saving Settings, replace title blocks on existing GMP shop-ticket sheets.</summary>
    public bool ApplyTitleBlockToExistingSheets { get; set; } = true;

    /// <summary>Margin (sheet feet) inset from the printable content area.</summary>
    public double SheetMarginFt { get; set; } = 0.25;
}
