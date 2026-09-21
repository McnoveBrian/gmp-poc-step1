namespace GmpPlatform.Core.Models;

/// <summary>Snapshot of a selected precast wall panel used to plan a shop ticket.</summary>
public sealed class WallPanelSnapshot
{
    public long ElementId { get; set; }
    public string UniqueId { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string FamilyName { get; set; } = "";
    public string TypeName { get; set; } = "";
    public string Mark { get; set; } = "";
    public string DesignNo { get; set; } = "";
    public double LengthFt { get; set; }
    public double HeightFt { get; set; }
    public double ThicknessFt { get; set; }
    public string DocumentTitle { get; set; } = "";
    public IReadOnlyList<string> NeighborMarks { get; set; } = Array.Empty<string>();
    public IReadOnlyList<PartQuantity> Parts { get; set; } = Array.Empty<PartQuantity>();
}

public sealed class PartQuantity
{
    public string PartNumber { get; set; } = "";
    public double Quantity { get; set; }
    public string Source { get; set; } = "model";
}

public enum ShopTicketSheetKind
{
    TifFabrication = 1,
    BifFinish = 2,
    SectionsAndBom = 3
}

public sealed class ShopTicketSheetPlan
{
    public ShopTicketSheetKind Kind { get; set; }
    public string SheetNumber { get; set; } = "";
    public string SheetName { get; set; } = "";
    public string Title { get; set; } = "";
    public IReadOnlyList<string> ProductionNotes { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> PlannedViews { get; set; } = Array.Empty<string>();
}

public sealed class ShopTicketPlan
{
    public string RunId { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; }
    public WallPanelSnapshot Panel { get; set; } = new();
    public IReadOnlyList<ShopTicketSheetPlan> Sheets { get; set; } = Array.Empty<ShopTicketSheetPlan>();
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
    public string DemoNote { get; set; } =
        "Demo build: creates a 3-sheet Revit package with drafting views, sample Detail Lines, " +
        "and a section cut when possible. Full AFIP-style detailing comes in later iterations.";
}

public sealed class ShopTicketRunResult
{
    public string RunId { get; set; } = "";
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public IReadOnlyList<string> CreatedSheetNames { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> CreatedViewNames { get; set; } = Array.Empty<string>();
    /// <summary>Element ids of sheets created in this run (for opening tabs after generation).</summary>
    public IReadOnlyList<long> CreatedSheetIds { get; set; } = Array.Empty<long>();
    public string? ReportPath { get; set; }
    public string? ExportFolder { get; set; }
}
