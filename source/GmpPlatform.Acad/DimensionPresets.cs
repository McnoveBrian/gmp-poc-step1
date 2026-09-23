using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;

namespace GmpPlatform.Acad;

/// <summary>
/// The dimension overrides the client's drafters actually apply.
/// <para>
/// 269 of the 276 dimensions on AFIP-1035 carry per-object overrides, which looks like
/// chaos until you cluster them: they reduce to 23 distinct override sets, of which 8
/// cover 90% and one covers 41%. So the engine needs a short list of named presets, not
/// per-dimension decisions. Values are measured from the approved drawing. All eight are
/// encoded; the remaining 15 sets account for 10% between them, in ones and twos.
/// </para>
/// <para>
/// Note DIMCLRT = green on nearly every preset even though the Tindall-4 style itself
/// specifies magenta: the drafters recolour dimension text as a plotting convention.
/// </para>
/// </summary>
internal sealed class DimensionPreset
{
    public required string Name { get; init; }

    /// <summary>How many of the 269 overridden dimensions used exactly this set.</summary>
    public required int ObservedCount { get; init; }

    public double? Dimasz { get; init; }
    public double? Dimexe { get; init; }
    public double? Dimexo { get; init; }
    public double? Dimgap { get; init; }
    public double? Dimtxt { get; init; }
    public double? Dimscale { get; init; }
    public int? Dimatfit { get; init; }
    public short DimclrtIndex { get; init; } = 3; // green

    /// <summary>
    /// Applies the preset. <paramref name="annotationScale"/> stands in for the measured
    /// DIMSCALE of 0: on the approved drawing those dimensions are annotative and take
    /// their size from the viewport, but an object created outside an annotative context
    /// renders its text at zero height — visually, a dimension with no number. Setting the
    /// explicit viewport scale (64 for the 3/16" = 1'-0" main view) is the correct
    /// equivalent until the engine creates annotative objects properly.
    /// </summary>
    public void ApplyTo(Dimension dimension, double annotationScale)
    {
        if (Dimasz.HasValue) dimension.Dimasz = Dimasz.Value;
        if (Dimexe.HasValue) dimension.Dimexe = Dimexe.Value;
        if (Dimexo.HasValue) dimension.Dimexo = Dimexo.Value;
        if (Dimgap.HasValue) dimension.Dimgap = Dimgap.Value;
        if (Dimtxt.HasValue) dimension.Dimtxt = Dimtxt.Value;
        if (Dimatfit.HasValue) dimension.Dimatfit = Dimatfit.Value;

        var scale = Dimscale is null or 0 ? annotationScale : Dimscale.Value;
        if (scale > 0) dimension.Dimscale = scale;

        dimension.Dimclrt = Color.FromColorIndex(ColorMethod.ByAci, DimclrtIndex);
    }
}

internal static class DimensionPresets
{
    /// <summary>Stub / ordinate chains — the most common set on the drawing (111 of 269).</summary>
    public static readonly DimensionPreset Stub = new()
    {
        Name = "stub-chain",
        ObservedCount = 111,
        Dimasz = 0.046875,
        Dimexe = 0.03125,
        Dimexo = 0.125,
        Dimgap = 0.03125,
        Dimtxt = 0.078125,
        Dimscale = 0
    };

    /// <summary>General linear dimensioning (49 of 269).</summary>
    public static readonly DimensionPreset Linear = new()
    {
        Name = "linear",
        ObservedCount = 49,
        Dimasz = 0.046875,
        Dimexe = 0.025,
        Dimexo = 0.125,
        Dimgap = 0.015625,
        Dimscale = 0
    };

    /// <summary>As linear, but scale left at the style value (26 of 269).</summary>
    public static readonly DimensionPreset LinearStyleScale = new()
    {
        Name = "linear-style-scale",
        ObservedCount = 26,
        Dimasz = 0.046875,
        Dimexe = 0.025,
        Dimexo = 0.125,
        Dimgap = 0.015625
    };

    /// <summary>Tight dimensions that move text rather than the arrows (21 of 269).</summary>
    public static readonly DimensionPreset TightFit = new()
    {
        Name = "tight-fit",
        ObservedCount = 21,
        Dimasz = 0.046875,
        Dimexe = 0.025,
        Dimexo = 0.125,
        Dimgap = 0.015625,
        Dimatfit = 1,
        Dimscale = 0
    };

    /// <summary>Overall / outermost dimension strings (16 of 269).</summary>
    public static readonly DimensionPreset Overall = new()
    {
        Name = "overall",
        ObservedCount = 16,
        Dimexo = 0.1,
        Dimgap = 0.03125,
        Dimscale = 0
    };

    /// <summary>
    /// Short dimensions hard against the geometry — extension lines start at the object
    /// (DIMEXO 0) rather than standing off it (8 of 269). The example on the approved sheet
    /// is a 2¼" dimension, where an offset would be most of the dimension.
    /// </summary>
    public static readonly DimensionPreset CloseOffset = new()
    {
        Name = "close-offset",
        ObservedCount = 8,
        Dimasz = 0.046875,
        Dimexe = 0.03125,
        Dimexo = 0,
        Dimgap = 0.03125,
        Dimtxt = 0.078125,
        Dimscale = 0
    };

    /// <summary>
    /// As <see cref="Linear"/> but with the extension-line offset left at the style value
    /// (7 of 269). Used on the full-length 46'-3" strings.
    /// </summary>
    public static readonly DimensionPreset LinearStyleOffset = new()
    {
        Name = "linear-style-offset",
        ObservedCount = 7,
        Dimasz = 0.046875,
        Dimexe = 0.025,
        Dimgap = 0.015625,
        Dimscale = 0
    };

    /// <summary>
    /// The lightest set: text gap and colour only, everything else from the style (5 of 269).
    /// Seen on ordinate dimensions.
    /// </summary>
    public static readonly DimensionPreset GapOnly = new()
    {
        Name = "gap-only",
        ObservedCount = 5,
        Dimgap = 0.03125,
        Dimscale = 0
    };

    public static readonly IReadOnlyList<DimensionPreset> All = new[]
    {
        Stub, Linear, LinearStyleScale, TightFit, Overall, CloseOffset, LinearStyleOffset, GapOnly
    };

    /// <summary>
    /// Share of the approved drawing's overridden dimensions this catalogue *describes*.
    /// Not the share the engine reproduces — see <see cref="InUse"/>.
    /// </summary>
    public static double CoverageFraction => All.Sum(p => p.ObservedCount) / 269.0;

    /// <summary>
    /// The presets the engine actually applies today, one per kind of dimension it draws:
    /// ordinate stub chains, overall strings, and the tight dimensions inside sections. The
    /// rest of the catalogue is measured and waiting for the call sites that need it.
    /// </summary>
    public static readonly IReadOnlyList<DimensionPreset> InUse = new[] { Stub, Overall, TightFit };
}
