namespace GmpPlatform.Core.Tickets;

/// <summary>
/// Everything the AutoCAD side needs to draw one shop ticket, and nothing about how
/// it is drawn. This is the hand-off contract: Revit fills it, AutoCAD consumes it,
/// and neither project references the other.
/// <para>
/// Units are inches in panel-local 2D coordinates, matching the DWG (INSUNITS = Inches,
/// all drafting at 1:1 in model space). Origin is the START PANEL end at the bottom of
/// the piece, X running along the panel length, Y up the panel height — the origin the
/// stub dimensions measure from.
/// </para>
/// </summary>
public sealed class PieceTicket
{
    public string SchemaVersion { get; set; } = "1.0";
    public string GeneratedUtc { get; set; } = "";
    public string SourceModel { get; set; } = "";

    public PieceIdentity Identity { get; set; } = new();
    public PieceDimensions Dimensions { get; set; } = new();
    public PieceGeometry Geometry { get; set; } = new();
    public List<TicketPart> Parts { get; set; } = new();
    public PieceQuantities Quantities { get; set; } = new();

    /// <summary>Anything unresolved. These must reach the drawing, not be swallowed.</summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>Values the title block prints.</summary>
public sealed class PieceIdentity
{
    public string Mark { get; set; } = "";          // CONTROL_MARK      -> MARK
    public string Pid { get; set; } = "";           // CONTROL_NUMBER    -> PID
    public string DesignNo { get; set; } = "";      // DESIGN_NUMBER     -> DESNO
    public string Code { get; set; } = "";          // CODE              -> CODE
    public string JobNumber { get; set; } = "";     // project number    -> JOB_NO
    public string ProjectName { get; set; } = "";   //                   -> PROJECT
    public string Plant { get; set; } = "";         // precast manufacturer -> PLANT
    public string DrawnBy { get; set; } = "";
    public string CheckedBy { get; set; } = "";
    public string ProjectLead { get; set; } = "";
    public string EngineerChecked { get; set; } = "";
    public string ReleasedForJobUse { get; set; } = "";
    public string Revision { get; set; } = "";
    public int RequiredCount { get; set; } = 1;
    public string FamilyName { get; set; } = "";
    public string TypeName { get; set; } = "";
    public long ElementId { get; set; }
}

/// <summary>Sizes as the ticket prints them, in inches.</summary>
public sealed class PieceDimensions
{
    public double LengthIn { get; set; }
    public double HeightIn { get; set; }
    public double ThicknessIn { get; set; }
    public double WytheInnerIn { get; set; }
    public double WytheInsulationIn { get; set; }
    public double WytheOuterIn { get; set; }
    public double InsulationLengthIn { get; set; }
    public double InsulationHeightIn { get; set; }
    public double SolidZoneTopIn { get; set; }
    public double SolidZoneBottomIn { get; set; }
    public double SolidZoneEnd1In { get; set; }
    public double SolidZoneEnd2In { get; set; }

    /// <summary>"parameters" when read from DIM_*, "bounding box" when approximated.</summary>
    public string Source { get; set; } = "";
}

/// <summary>Drawable outlines, in panel-local inches.</summary>
public sealed class PieceGeometry
{
    /// <summary>Outer boundary of the panel face, closed.</summary>
    public List<Point2> Outline { get; set; } = new();

    /// <summary>Inner loops: openings, blockouts and recesses cut through the face.</summary>
    public List<List<Point2>> Openings { get; set; } = new();

    public double FaceAreaSqFt { get; set; }
    public int FaceLoopCount { get; set; }

    /// <summary>45-degree faces measured on the piece. See <see cref="AngledFace"/>.</summary>
    public List<AngledFace> AngledFaces { get; set; } = new();
}

/// <summary>
/// A 45-degree face on the piece — real geometry, reported without being told what it means.
/// <para>
/// On AFIP-1035 these measure a 4.24" leg over an 8" edge, which matches the corbel slope the
/// approved Section "M" dimensions as 8" + 8" SLOPE. They are **not** the ½" chamfers the
/// ticket calls out along its edges: those are not modelled at all. Naming these "chamfers"
/// would put a wrong callout on the drawing, so the measurement is reported and the
/// interpretation left to GMP.
/// </para>
/// </summary>
public sealed class AngledFace
{
    /// <summary>Leg length of the 45-degree face, in inches.</summary>
    public double LegIn { get; set; }

    /// <summary>Length of the angled edge, in inches.</summary>
    public double LengthIn { get; set; }

    public double AreaSqIn { get; set; }
}

public sealed class Point2
{
    public double X { get; set; }
    public double Y { get; set; }

    public Point2() { }
    public Point2(double x, double y) { X = x; Y = y; }
}

/// <summary>One BOM line, plus where its instances sit so they can be drawn and dimensioned.</summary>
public sealed class TicketPart
{
    public string PartNumber { get; set; } = "";
    public double Quantity { get; set; }

    /// <summary>
    /// The item's description as the model states it, for the second line of a callout —
    /// the approved ticket reads <c>℄ (1) INS087 (TIF)</c> over <c>½" RECESS</c>.
    /// Empty when the model has nothing to say; never invented.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>Which parameter <see cref="Description"/> came from, so it can be checked.</summary>
    public string DescriptionSource { get; set; } = "";

    /// <summary>"contained", "connection assembly", "derived" — shown on the drawing.</summary>
    public string Source { get; set; } = "";

    /// <summary>True when we computed it rather than read it; drives the DERIVED marking.</summary>
    public bool IsDerived { get; set; }

    public List<PartInstance> Instances { get; set; } = new();
}

/// <summary>A single placed part, in panel-local inches.</summary>
public sealed class PartInstance
{
    public double X { get; set; }
    public double Y { get; set; }

    /// <summary>"TIF", "BIF" or "SIF" — which face of the panel the item belongs to.</summary>
    public string Face { get; set; } = "";

    /// <summary>
    /// Distance from the reference face along the thickness direction, in inches.
    /// Kept so the face assignment can be checked rather than trusted: the TIF/BIF
    /// convention is a client rule we have not had confirmed.
    /// </summary>
    public double DepthIn { get; set; }

    public double RotationDeg { get; set; }
    public long ElementId { get; set; }
}

/// <summary>Concrete, insulation and weight figures for the BOM block.</summary>
public sealed class PieceQuantities
{
    public double TopFaceAreaSqFt { get; set; }
    public double BottomFaceAreaSqFt { get; set; }
    public double TopConcreteCuYd { get; set; }
    public double BottomConcreteCuYd { get; set; }
    public double InsulationSqFt { get; set; }
    public double WeightLb { get; set; }
    public double DensityPcf { get; set; }
    public string TopMixCode { get; set; } = "";
    public string BottomMixCode { get; set; } = "";
    public int ReleaseStrengthPsi { get; set; }
    public int TwentyEightDayStrengthPsi { get; set; }

    /// <summary>Figures we computed rather than read; these get the DERIVED treatment.</summary>
    public List<string> Derived { get; set; } = new();
}
