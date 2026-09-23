using GmpPlatform.Core.Tickets;
using Xunit;

namespace GmpPlatform.Core.Tests;

public class TicketUnitsTests
{
    /// <summary>
    /// The regression that started this: Revit returns 46'-3" as 46.24999999999953 feet, which
    /// multiplies out to 554.9999999999432". The rebar planner floored that to 554", named the
    /// bar RSX05096 instead of RSX05097, and the wrong part number reached the bill of
    /// material. Rounding at the contract boundary is what stops it.
    /// </summary>
    [Fact]
    public void Feet_to_inches_absorbs_revit_floating_point_noise()
    {
        const double fortySixFeetThreeInches = 46.24999999999953;
        Assert.Equal(555.0, TicketUnits.Inches(fortySixFeetThreeInches));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 12)]
    [InlineData(12.145833333, 145.75)]   // AFIP-1035 height, 12'-1¾"
    public void Feet_to_inches_converts(double feet, double expectedInches)
    {
        Assert.Equal(expectedInches, TicketUnits.Inches(feet), 3);
    }

    [Theory]
    [InlineData(555, "46'-3\"")]
    [InlineData(543, "45'-3\"")]
    [InlineData(12, "1'-0\"")]
    [InlineData(0, "0'-0\"")]
    [InlineData(145.75, "12'-1.75\"")]
    public void Inches_format_the_way_the_ticket_writes_them(double inches, string expected)
    {
        Assert.Equal(expected, TicketUnits.FeetInches(inches));
    }

    [Theory]
    [InlineData(574.0, "47'-10\"")]      // FIP-1244 length, 47.833 ft
    [InlineData(148.25, "12'-4 1/4\"")]  // FIP-1244 height, 12.354 ft
    [InlineData(9.0, "9\"")]             // thickness: inches alone under a foot
    [InlineData(145.75, "12'-1 3/4\"")]  // AFIP-1035 height, as the ticket prints it
    [InlineData(555.0, "46'-3\"")]
    [InlineData(12.0, "1'-0\"")]
    [InlineData(0.5, "1/2\"")]
    [InlineData(47.875, "3'-11 7/8\"")]  // a value on the approved ticket
    public void Architectural_writes_lengths_the_way_a_drafter_does(double inches, string expected)
    {
        Assert.Equal(expected, TicketUnits.Architectural(inches));
    }

    [Fact]
    public void Architectural_rounds_to_the_nearest_eighth_and_carries_into_the_next_foot()
    {
        Assert.Equal("4'-0\"", TicketUnits.Architectural(47.97));   // 47.97" rounds to 48"
        Assert.Equal("2 1/4\"", TicketUnits.Architectural(2.26));
    }

    [Fact]
    public void Architectural_reads_decimal_feet_from_revit_correctly()
    {
        // What the dialog used to print, and what it prints now.
        Assert.Equal("47'-10\"", TicketUnits.Architectural(TicketUnits.Inches(47.833333333)));
        Assert.Equal("9\"", TicketUnits.Architectural(TicketUnits.Inches(0.75)));
    }

    /// <summary>
    /// A dimension a hair under a whole foot must not print as 45'-12".
    /// </summary>
    [Fact]
    public void Almost_a_whole_foot_rounds_up_into_the_next_foot()
    {
        Assert.Equal("46'-0\"", TicketUnits.FeetInches(551.995));
        Assert.Equal("46'-0\"", TicketUnits.FeetInches(552.0));
    }
}
