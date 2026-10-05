using GmpPlatform.Core.Interop;
using Xunit;

namespace GmpPlatform.Core.Tests;

public class AcadRunnerTests
{
    private const string Acad2025 = @"C:\Program Files\Autodesk\AutoCAD 2025\accoreconsole.exe";
    private const string Acad2026 = @"C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe";
    private const string Acad2027 = @"C:\Program Files\Autodesk\AutoCAD 2027\accoreconsole.exe";

    /// <summary>
    /// The regression behind the supported list: taking the newest install sent the engine to
    /// AutoCAD 2027, a different API family, and the run stopped without saving.
    /// </summary>
    [Fact]
    public void A_supported_release_wins_over_a_newer_unsupported_one()
    {
        Assert.Equal(Acad2025, AcadRunner.ChooseConsole(new[] { Acad2027, Acad2025 }));
        Assert.Equal(Acad2026, AcadRunner.ChooseConsole(new[] { Acad2027, Acad2026 }));
    }

    [Fact]
    public void The_release_the_engine_is_built_against_is_preferred()
    {
        Assert.Equal(Acad2025, AcadRunner.ChooseConsole(new[] { Acad2026, Acad2027, Acad2025 }));
    }

    [Fact]
    public void Without_a_supported_release_the_newest_install_is_used()
    {
        const string acad2024 = @"C:\Program Files\Autodesk\AutoCAD 2024\accoreconsole.exe";
        Assert.Equal(Acad2027, AcadRunner.ChooseConsole(new[] { acad2024, Acad2027 }));
        Assert.False(AcadRunner.IsSupported(Acad2027));
    }

    [Fact]
    public void No_install_gives_no_console()
    {
        Assert.Null(AcadRunner.ChooseConsole(Array.Empty<string>()));
    }

    [Theory]
    [InlineData(Acad2025, "2025")]
    [InlineData(@"D:\Apps\Autodesk\AutoCAD 2026\accoreconsole.exe", "2026")]
    [InlineData(@"C:\Tools\accoreconsole.exe", null)]
    public void The_release_is_read_from_the_install_folder(string path, string? expected)
    {
        Assert.Equal(expected, AcadRunner.ReleaseOf(path));
    }

    [Fact]
    public void Both_releases_of_the_R25_family_are_supported()
    {
        Assert.True(AcadRunner.IsSupported(Acad2025));
        Assert.True(AcadRunner.IsSupported(Acad2026));
        Assert.False(AcadRunner.IsSupported(@"C:\Tools\accoreconsole.exe"));
    }
}
