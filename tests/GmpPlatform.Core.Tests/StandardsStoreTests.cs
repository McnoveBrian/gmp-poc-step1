using GmpPlatform.Core.Settings;
using Xunit;

namespace GmpPlatform.Core.Tests;

public class StandardsStoreTests
{
    [Fact]
    public void Built_in_profile_is_the_approved_seed_standard()
    {
        var p = new StandardsProfile();

        Assert.Equal("PANEL", p.Layers.Panel);
        Assert.Equal("INSULATION HATCH", p.Layers.InsulationHatch);
        Assert.Equal("Tindall-4", p.Styles.Dimension);
        Assert.Equal("ANSI37", p.Styles.InsulationHatchPattern);
        Assert.Equal(5.0, p.Styles.InsulationHatchScale);
        Assert.Equal("SecCut_w_PlanDetail_TAG", p.Blocks.SectionMarker);
        Assert.Equal("SEC#", p.Blocks.SectionMarkerLetterTag);
        Assert.Equal("T_S_BOM", p.Blocks.BomRow);
        Assert.Equal("JOB_NO", p.TitleBlock.JobNumber);
        Assert.Equal("28DAY", p.TitleBlock.TwentyEightDayStrength);
        Assert.Equal("MARK END", p.Labels.MarkEnd);
    }

    /// <summary>A client profile lists only what differs; everything else stays Tindall.</summary>
    [Fact]
    public void Partial_profile_keeps_every_unlisted_default()
    {
        var p = StandardsStore.Parse("""
            {
              // comments and trailing commas are allowed: drafters edit this by hand
              "name": "Plant B",
              "layers": { "text": "A-TEXT", },
              "titleBlock": { "JOB_NO_ignored_unknown_key": "x", "jobNumber": "JOBNO" },
            }
            """);

        Assert.Equal("Plant B", p.Name);
        Assert.Equal("A-TEXT", p.Layers.Text);
        Assert.Equal("PANEL", p.Layers.Panel);
        Assert.Equal("JOBNO", p.TitleBlock.JobNumber);
        Assert.Equal("MARK", p.TitleBlock.Mark);
        Assert.Equal("Tindall", p.Styles.Text);
        Assert.Equal("SEC#", p.Blocks.SectionMarkerLetterTag);
        Assert.Equal("TIF VIEW", p.Labels.TifView);
    }

    [Fact]
    public void Null_section_means_not_given_rather_than_no_names()
    {
        var p = StandardsStore.Parse("""{ "layers": null, "blocks": null, "labels": null }""");

        Assert.Equal("PANEL", p.Layers.Panel);
        Assert.Equal("T_SHOP", p.Blocks.TitleBlockPrefix);
        Assert.Equal("SOLID ZONE", p.Labels.SolidZone);
    }

    [Fact]
    public void Property_names_are_case_insensitive()
    {
        var p = StandardsStore.Parse("""{ "Styles": { "InsulationHatchScale": 7.5, "DIMENSION": "Plant-4" } }""");

        Assert.Equal(7.5, p.Styles.InsulationHatchScale);
        Assert.Equal("Plant-4", p.Styles.Dimension);
    }

    [Fact]
    public void Defaults_json_round_trips_to_the_built_in_profile()
    {
        var json = StandardsStore.DefaultsJson();
        var p = StandardsStore.Parse(json);

        Assert.Contains("\"sectionMarkerLetterTag\": \"SEC#\"", json);
        Assert.Equal(new StandardsProfile().Labels.BifView, p.Labels.BifView);
        Assert.Equal(new StandardsProfile().TitleBlock.ReleaseStrength, p.TitleBlock.ReleaseStrength);
    }

    [Fact]
    public void Missing_configured_file_falls_back_to_defaults_and_says_why()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gmp-no-such-{System.Guid.NewGuid():N}.json");

        var p = StandardsStore.Load(new PlatformSettings { StandardsProfilePath = path });

        Assert.Equal("PANEL", p.Layers.Panel);
        Assert.Null(StandardsStore.LastLoadedFrom);
        Assert.Contains("does not exist", StandardsStore.LastLoadError);
    }

    [Fact]
    public void Broken_file_falls_back_to_defaults_and_says_why()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gmp-broken-{System.Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ \"layers\": { \"panel\": ");
        try
        {
            var p = StandardsStore.Load(new PlatformSettings { StandardsProfilePath = path });

            Assert.Equal("PANEL", p.Layers.Panel);
            Assert.Null(StandardsStore.LastLoadedFrom);
            Assert.StartsWith(path, StandardsStore.LastLoadError);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Configured_file_is_read_and_reported()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gmp-standards-{System.Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{ "blocks": { "bomRow": "PLANT_BOM" } }""");
        try
        {
            var p = StandardsStore.Load(new PlatformSettings { StandardsProfilePath = path });

            Assert.Equal("PLANT_BOM", p.Blocks.BomRow);
            Assert.Equal(path, StandardsStore.LastLoadedFrom);
            Assert.Null(StandardsStore.LastLoadError);
        }
        finally { File.Delete(path); }
    }
}
