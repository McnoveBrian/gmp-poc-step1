using System.Text.Json;
using System.Text.Json.Serialization;

namespace GmpPlatform.Core.Tickets;

/// <summary>Serialisation for the Revit -> AutoCAD hand-off.</summary>
public static class TicketJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(PieceTicket ticket) => JsonSerializer.Serialize(ticket, Options);

    public static PieceTicket? Deserialize(string json) =>
        JsonSerializer.Deserialize<PieceTicket>(json, Options);

    /// <summary>Writes the ticket and returns the file path.</summary>
    public static string Write(PieceTicket ticket, string folder, string? fileName = null)
    {
        Directory.CreateDirectory(folder);
        var name = fileName ?? Sanitize(
            string.IsNullOrWhiteSpace(ticket.Identity.Mark) ? "piece" : ticket.Identity.Mark) + ".ticket.json";
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, Serialize(ticket));
        return path;
    }

    public static PieceTicket Read(string path) =>
        Deserialize(File.ReadAllText(path)) ?? throw new InvalidDataException($"Not a piece ticket: {path}");

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
