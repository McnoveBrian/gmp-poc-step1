using GmpPlatform.Core.Tickets;
using Xunit;

namespace GmpPlatform.Core.Tests;

/// <summary>
/// The hand-off contract. Revit writes it, AutoCAD reads it, and nothing else connects the two
/// halves — so anything that survives the round trip is what the drawing gets, and anything
/// that does not is silently lost between the projects.
/// </summary>
public class TicketJsonTests
{
    private static PieceTicket Sample() => new()
    {
        SourceModel = "45431_Pilgrims_RTE_COPY",
        Identity = { Mark = "AFIP-1035", Pid = "1035", DesignNo = "D-70", JobNumber = "45431" },
        Dimensions = { LengthIn = 555, HeightIn = 145.75, ThicknessIn = 9, InsulationLengthIn = 543 },
        Geometry =
        {
            Outline = { new Point2(0, 0), new Point2(555, 0), new Point2(555, 145.75), new Point2(0, 145.75) },
            FaceAreaSqFt = 533.3043,
            AngledFaces = { new AngledFace { LegIn = 4.243, LengthIn = 8, AreaSqIn = 48 } }
        },
        Parts =
        {
            new TicketPart
            {
                PartNumber = "INS087",
                Quantity = 8,
                Description = "Mini V Connector 80Degree",
                DescriptionSource = "type.IDENTITY_DESCRIPTION",
                Instances = { new PartInstance { X = 30, Y = 145.8, Face = "TIF", DepthIn = -0.501 } }
            }
        },
        Quantities = { BottomConcreteCuYd = 4.94, Derived = { "rebar rule-generated" } },
        Warnings = { "4 angled (45-degree) face(s) measured" }
    };

    [Fact]
    public void Round_trip_preserves_everything_the_drawing_needs()
    {
        var back = TicketJson.Deserialize(TicketJson.Serialize(Sample()));

        Assert.NotNull(back);
        Assert.Equal("AFIP-1035", back!.Identity.Mark);
        Assert.Equal("D-70", back.Identity.DesignNo);
        Assert.Equal(555, back.Dimensions.LengthIn);
        Assert.Equal(145.75, back.Dimensions.HeightIn);
        Assert.Equal(533.3043, back.Geometry.FaceAreaSqFt);
        Assert.Equal(4, back.Geometry.Outline.Count);
        Assert.Equal(4.94, back.Quantities.BottomConcreteCuYd);
    }

    [Fact]
    public void Round_trip_preserves_part_positions_and_faces()
    {
        var back = TicketJson.Deserialize(TicketJson.Serialize(Sample()))!;
        var part = Assert.Single(back.Parts);

        Assert.Equal("INS087", part.PartNumber);
        Assert.Equal(8, part.Quantity);

        var instance = Assert.Single(part.Instances);
        Assert.Equal(30, instance.X);
        Assert.Equal(145.8, instance.Y);
        Assert.Equal("TIF", instance.Face);
        Assert.Equal(-0.501, instance.DepthIn);
    }

    /// <summary>
    /// Warnings are how the engine tells the drawing what it could not resolve. Losing them in
    /// serialisation would turn flagged guesses into silent ones, which is the failure this
    /// whole design is built to avoid.
    /// </summary>
    [Fact]
    public void Round_trip_preserves_warnings_and_derived_markers()
    {
        var back = TicketJson.Deserialize(TicketJson.Serialize(Sample()))!;

        Assert.Single(back.Warnings);
        Assert.Contains("angled", back.Warnings[0]);
        Assert.Single(back.Quantities.Derived);
    }

    [Fact]
    public void Round_trip_preserves_the_description_and_where_it_came_from()
    {
        var part = Assert.Single(TicketJson.Deserialize(TicketJson.Serialize(Sample()))!.Parts);

        Assert.Equal("Mini V Connector 80Degree", part.Description);
        Assert.Equal("type.IDENTITY_DESCRIPTION", part.DescriptionSource);
    }

    [Fact]
    public void Round_trip_preserves_measured_angled_faces()
    {
        var face = Assert.Single(TicketJson.Deserialize(TicketJson.Serialize(Sample()))!.Geometry.AngledFaces);

        Assert.Equal(4.243, face.LegIn);
        Assert.Equal(8, face.LengthIn);
    }

    [Fact]
    public void Read_and_write_use_the_mark_as_the_file_name()
    {
        var folder = Path.Combine(Path.GetTempPath(), "gmp-ticket-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var path = TicketJson.Write(Sample(), folder);
            Assert.Equal("AFIP-1035.ticket.json", Path.GetFileName(path));
            Assert.Equal("AFIP-1035", TicketJson.Read(path).Identity.Mark);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_file_that_is_not_a_ticket_fails_loudly()
    {
        var folder = Path.Combine(Path.GetTempPath(), "gmp-ticket-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "junk.json");
        try
        {
            File.WriteAllText(path, "null");
            Assert.Throws<InvalidDataException>(() => TicketJson.Read(path));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
