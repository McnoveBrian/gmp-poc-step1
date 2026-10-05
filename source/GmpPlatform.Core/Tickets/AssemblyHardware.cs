namespace GmpPlatform.Core.Tickets;

/// <summary>
/// Items that are hardware of another part rather than items in their own right: the welded
/// studs and plates (EMS…) that travel with an insert. The approved AFIP-1035 ticket carries
/// eight INS087 inserts, each with EMS016, EMS018 and EMS325 at exactly its location, and lists
/// none of the EMS items — no block, no callout, no bill-of-material row.
/// <para>
/// The rule is deliberately narrow: an EMS part is hardware only when <b>every</b> instance of
/// it sits on an instance of a part that is not itself hardware. An EMS item standing alone is
/// still listed. Other families (e.g. SES items beside an SPS plate) are not assumed to roll up
/// without a sample that shows it.
/// </para>
/// </summary>
public static class AssemblyHardware
{
    /// <summary>Part-number prefixes of hardware families.</summary>
    public static readonly IReadOnlyList<string> Prefixes = new[] { "EMS" };

    /// <summary>Two instances closer than this (inches, in plan) are at the same location.</summary>
    public const double SameLocationIn = 1.0;

    public static bool IsHardwareFamily(string partNumber) =>
        Prefixes.Any(p => partNumber.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Hardware parts, each with the part it belongs to (the first non-hardware part, by part
    /// number, at its first instance).
    /// </summary>
    public static IReadOnlyDictionary<string, string> Find(IReadOnlyList<TicketPart> parts)
    {
        var hosts = parts
            .Where(p => !IsHardwareFamily(p.PartNumber) && p.Instances.Count > 0)
            .OrderBy(p => p.PartNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();

        TicketPart? HostAt(PartInstance at) => hosts.FirstOrDefault(h => h.Instances.Any(i =>
            Math.Abs(i.X - at.X) <= SameLocationIn && Math.Abs(i.Y - at.Y) <= SameLocationIn));

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in parts.Where(p => IsHardwareFamily(p.PartNumber) && p.Instances.Count > 0))
        {
            var hostsOfEach = part.Instances.Select(HostAt).ToList();
            if (hostsOfEach.All(h => h != null))
                result[part.PartNumber] = hostsOfEach[0]!.PartNumber;
        }

        return result;
    }
}
