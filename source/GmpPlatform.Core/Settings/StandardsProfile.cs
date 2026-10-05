using System.Text.Json;
using System.Text.Json.Serialization;

namespace GmpPlatform.Core.Settings;

/// <summary>
/// The client drafting standard the drawing engine writes to: layer, style, block and
/// attribute names, and the fixed wording on the sheet.
/// <para>
/// Every default is what the approved Tindall / AFIP-1035 seed uses, so with no profile file
/// the engine draws exactly as before. A different seed drawing, plant or client standard is
/// a JSON file (<see cref="StandardsStore"/>), not a code change. Only names live here; the
/// measured layout conventions (offsets, station direction) stay in
/// <c>GmpPlatform.Core.Tickets.SheetConventions</c>, where tests guard them.
/// </para>
/// </summary>
public sealed class StandardsProfile
{
    /// <summary>Human-readable name, reported in the draw log.</summary>
    public string Name { get; set; } = "Tindall (AFIP-1035 seed) - built-in";

    public LayerNames Layers { get; set; } = new();
    public StyleNames Styles { get; set; } = new();
    public BlockNames Blocks { get; set; } = new();

    /// <summary>Title-block attribute tags, by the ticket field that fills them.</summary>
    public TitleBlockTags TitleBlock { get; set; } = new();

    public SheetLabels Labels { get; set; } = new();

    public sealed class LayerNames
    {
        public string Panel { get; set; } = "PANEL";
        public string Dimensions { get; set; } = "DIMENSIONS";
        public string Text { get; set; } = "TEXT";
        public string Insert { get; set; } = "INSERT";
        public string Insulation { get; set; } = "INSULATION";
        public string InsulationHatch { get; set; } = "INSULATION HATCH";
        public string Tags { get; set; } = "TAGS";

        /// <summary>Content the engine computed rather than read; red so it is reviewed.</summary>
        public string Derived { get; set; } = "GMP-DERIVED";
    }

    public sealed class StyleNames
    {
        public string Dimension { get; set; } = "Tindall-4";
        public string Text { get; set; } = "Tindall";
        public string InsulationHatchPattern { get; set; } = "ANSI37";
        public double InsulationHatchScale { get; set; } = 5.0;
    }

    public sealed class BlockNames
    {
        /// <summary>Section cut marker placed on the TIF view.</summary>
        public string SectionMarker { get; set; } = "SecCut_w_PlanDetail_TAG";

        /// <summary>Attribute on the section marker that carries the section letter.</summary>
        public string SectionMarkerLetterTag { get; set; } = "SEC#";

        /// <summary>Bar symbol used for generated reinforcement.</summary>
        public string Rebar { get; set; } = "REBAR";

        /// <summary>Title-block blocks are those whose name starts with this.</summary>
        public string TitleBlockPrefix { get; set; } = "T_SHOP";

        /// <summary>One bill-of-material row.</summary>
        public string BomRow { get; set; } = "T_S_BOM";

        public string BomPartNumberTag { get; set; } = "PART_NO";
        public string BomQuantityTag { get; set; } = "QTY";
    }

    public sealed class TitleBlockTags
    {
        public string Mark { get; set; } = "MARK";
        public string Pid { get; set; } = "PID";
        public string DesignNo { get; set; } = "DESNO";
        public string Code { get; set; } = "CODE";
        public string JobNumber { get; set; } = "JOB_NO";
        public string Project { get; set; } = "PROJECT";
        public string Plant { get; set; } = "PLANT";
        public string DrawnBy { get; set; } = "DRN";
        public string CheckedBy { get; set; } = "CHK";
        public string ProjectLead { get; set; } = "PJ";
        public string EngineerChecked { get; set; } = "ECHK";
        public string ReleasedForJobUse { get; set; } = "JOBUSE";
        public string Revision { get; set; } = "REV";
        public string RequiredCount { get; set; } = "REQ_NO";
        public string Length { get; set; } = "LENGTH";
        public string Width { get; set; } = "WIDTH";
        public string Weight { get; set; } = "WT";
        public string ReleaseStrength { get; set; } = "REL";
        public string TwentyEightDayStrength { get; set; } = "28DAY";
    }

    public sealed class SheetLabels
    {
        public string TifView { get; set; } = "TIF VIEW";

        /// <summary>MText; <c>\U+2013</c> is an en dash.</summary>
        public string BifView { get; set; } = "BOTTOM FINISH LAYOUT \\U+2013 CONCRETE REMOVED";

        public string SifView { get; set; } = "SIF VIEW";
        public string StartInsulation { get; set; } = "START INSUL.";
        public string SolidZone { get; set; } = "SOLID ZONE";

        /// <summary>Insulation item quoted with the insulated length, e.g. "45'-3" (SNX100)".</summary>
        public string InsulationPartNumber { get; set; } = "SNX100";
        public string StartPanel { get; set; } = "START PANEL";
        public string StopPanel { get; set; } = "STOP PANEL";
        public string MarkEnd { get; set; } = "MARK END";

        /// <summary>Opening name before its numbers: "B.O.-" gives "B.O.-2,3".</summary>
        public string OpeningPrefix { get; set; } = "B.O.-";

        /// <summary>Heading of an opening's dimension column beside the piece: "BLOCKOUT-1".</summary>
        public string OpeningColumnPrefix { get; set; } = "BLOCKOUT-";

        /// <summary>Stub labels at an opening's two stations: "START B.O.-1", "STOP B.O.-1".</summary>
        public string OpeningStart { get; set; } = "START";
        public string OpeningStop { get; set; } = "STOP";
    }
}

/// <summary>
/// Finds the standards profile: <see cref="PlatformSettings.StandardsProfilePath"/> when set,
/// otherwise <c>standards.json</c> beside <c>settings.json</c>, otherwise the built-in Tindall
/// defaults. Missing values in a file keep their defaults, so a profile only lists what differs.
/// </summary>
public static class StandardsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>The default location: <c>standards.json</c> next to <c>settings.json</c>.</summary>
    public static string DefaultPath => Path.Combine(PlatformPaths.Config, "standards.json");

    /// <summary>Where the last <see cref="Load"/> read from, or null for the built-in defaults.</summary>
    public static string? LastLoadedFrom { get; private set; }

    /// <summary>Why the last <see cref="Load"/> fell back to defaults, or null.</summary>
    public static string? LastLoadError { get; private set; }

    public static StandardsProfile Load(PlatformSettings settings)
    {
        LastLoadedFrom = null;
        LastLoadError = null;

        var configured = settings.StandardsProfilePath;
        var path = !string.IsNullOrWhiteSpace(configured) ? configured! : DefaultPath;

        if (!File.Exists(path))
        {
            if (!string.IsNullOrWhiteSpace(configured))
                LastLoadError = $"StandardsProfilePath '{configured}' does not exist";
            return new StandardsProfile();
        }

        try
        {
            var profile = Parse(File.ReadAllText(path));
            LastLoadedFrom = path;
            return profile;
        }
        catch (Exception ex)
        {
            LastLoadError = $"{path}: {ex.GetType().Name}: {ex.Message}";
            return new StandardsProfile();
        }
    }

    /// <summary>Reads a profile; absent sections and values keep the built-in defaults.</summary>
    public static StandardsProfile Parse(string json)
    {
        var profile = JsonSerializer.Deserialize<StandardsProfile>(json, JsonOptions) ?? new StandardsProfile();

        // An explicit null for a section ("layers": null) means "not given", not "no names".
        profile.Layers ??= new StandardsProfile.LayerNames();
        profile.Styles ??= new StandardsProfile.StyleNames();
        profile.Blocks ??= new StandardsProfile.BlockNames();
        profile.TitleBlock ??= new StandardsProfile.TitleBlockTags();
        profile.Labels ??= new StandardsProfile.SheetLabels();
        return profile;
    }

    /// <summary>The built-in profile as JSON: a starting point for a client's own file.</summary>
    public static string DefaultsJson() => JsonSerializer.Serialize(new StandardsProfile(), JsonOptions);
}
