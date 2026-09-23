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

    // ---- AutoCAD hand-off -------------------------------------------------

    /// <summary>
    /// The client DWG used as the drawing template. Its layers, styles, blocks, title blocks
    /// and layouts are reused, so the generated ticket comes out in the house standard.
    /// </summary>
    public string? SeedDwgPath { get; set; }

    /// <summary>accoreconsole.exe. Left empty, the newest installed AutoCAD is found.</summary>
    public string? AutoCadConsolePath { get; set; }

    /// <summary>
    /// Optional font mapping (.fmp) for SHX fonts the client uses but this machine lacks.
    /// Without it, text in those styles plots blank — including every dimension value.
    /// </summary>
    public string? FontMapPath { get; set; }

    /// <summary>GmpPlatform.Acad.dll, when it is not beside the add-in.</summary>
    public string? AcadPluginPath { get; set; }

    /// <summary>Plot the generated sheets to PDF after drawing.</summary>
    public bool PlotAfterDrawing { get; set; } = true;

    // ---- Ribbon ---------------------------------------------------------------

    /// <summary>
    /// Shows the development-only buttons: <b>Wall Ticket</b>, <b>Export DWG</b> and
    /// <b>Verify</b>. Off by default.
    /// <para>
    /// Wall Ticket is the earlier route that builds sheets inside Revit from drafting views and
    /// placeholder text; it was retired from the demo because its output does not represent
    /// the product, and the AutoCAD route (Ticket Drawing) is what produces the client's
    /// ticket. Export DWG and Verify exist only to serve it — Verify generates those sheets, so
    /// clicking it in front of a client would put placeholder drawings on screen. The commands
    /// are kept rather than deleted: the client's model carries some seventy Revit ticket view
    /// templates, and if their team produces tickets in Revit, a Revit route built on those
    /// templates becomes Phase 1 scope.
    /// </para>
    /// </summary>
    public bool ShowDeveloperTools { get; set; }
}
