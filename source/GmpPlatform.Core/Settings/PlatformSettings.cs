namespace GmpPlatform.Core.Settings;

/// <summary>User-configurable options for shop-ticket generation.</summary>
public sealed class PlatformSettings
{
    /// <summary>Revit UniqueId of the preferred title-block FamilySymbol, when known.</summary>
    public string? TitleBlockUniqueId { get; set; }

    /// <summary>Display name "Family : Type" used when UniqueId is missing or stale.</summary>
    public string? TitleBlockFamilyAndType { get; set; }

    /// <summary>Clearance (ft, drafting space) between panel geometry and note columns.</summary>
    public double NoteClearanceFt { get; set; } = 1.0;

    /// <summary>Clearance (ft) between neighboring section cells on the sections sheet.</summary>
    public double SectionCellGapFt { get; set; } = 1.5;

    /// <summary>Fraction of sheet width reserved on the right for the title-block column.</summary>
    public double TitleBlockRightReserve { get; set; } = 0.28;
}
