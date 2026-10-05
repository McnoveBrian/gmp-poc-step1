using GmpPlatform.Core.Tickets;
using Xunit;

namespace GmpPlatform.Core.Tests;

public class AssemblyHardwareTests
{
    private static TicketPart Part(string number, params (double X, double Y)[] at) => new()
    {
        PartNumber = number,
        Quantity = at.Length,
        Instances = at.Select(a => new PartInstance { X = a.X, Y = a.Y, Face = "BIF" }).ToList()
    };

    /// <summary>FIP-1171 as exported: the INS087 insert with its three EMS items 0.4" away.</summary>
    [Fact]
    public void Ems_items_on_an_insert_are_its_hardware()
    {
        var parts = new List<TicketPart>
        {
            Part("EMS016", (24.1, -0.4)),
            Part("EMS018", (24.1, -0.4)),
            Part("EMS325", (24.1, -0.4)),
            Part("INS087", (24.1, 0.0)),
            Part("PAS018", (65.8, 36.0), (65.8, 109.5)),
        };

        var hardware = AssemblyHardware.Find(parts);

        Assert.Equal(new[] { "EMS016", "EMS018", "EMS325" }, hardware.Keys.OrderBy(k => k));
        Assert.All(hardware.Values, host => Assert.Equal("INS087", host));
    }

    [Fact]
    public void An_ems_item_standing_alone_is_still_listed()
    {
        var parts = new List<TicketPart>
        {
            Part("EMS016", (24.1, 0), (300, 50)),   // second one is nowhere near an insert
            Part("INS087", (24.1, 0)),
        };

        Assert.Empty(AssemblyHardware.Find(parts));
    }

    [Fact]
    public void Other_families_are_not_assumed_to_roll_up()
    {
        var parts = new List<TicketPart>
        {
            Part("SES010", (532.9, 145.9)),
            Part("SPS033", (533.0, 145.5)),
        };

        Assert.Empty(AssemblyHardware.Find(parts));
    }

    [Fact]
    public void Hardware_does_not_host_hardware()
    {
        var parts = new List<TicketPart>
        {
            Part("EMS016", (10, 10)),
            Part("EMS018", (10, 10)),
        };

        Assert.Empty(AssemblyHardware.Find(parts));
    }
}
