using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace GmpPlatform.Acad;

/// <summary>
/// Matches a Tindall part number to the client's own block, and sets the dynamic
/// visibility state for the face being drawn.
/// <para>
/// Their library uses one dynamic block per part with a visibility state per view
/// orientation — IN014 carries IN014_TIF / IN014_SIF_FT_BK / IN014_SIF_LT_RT, PA018
/// carries PAG018_TIF / PAG018_SIF. So placing a part is "insert the block, set the
/// state", not "pick a per-view block". Block names do not match part numbers exactly
/// (INS087 lives in block IN087), so matching is by letter prefix plus digits.
/// </para>
/// </summary>
internal sealed class PartBlockResolver
{
    private readonly Database _db;
    private readonly Transaction _tr;
    private readonly DrawLog _log;
    private readonly Dictionary<string, ObjectId> _byPart = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Name, ObjectId Id)> _blocks = new();
    private readonly HashSet<string> _reported = new(StringComparer.OrdinalIgnoreCase);

    public PartBlockResolver(Database db, Transaction tr, DrawLog log)
    {
        _db = db;
        _tr = tr;
        _log = log;

        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        foreach (ObjectId id in bt)
        {
            var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
            if (btr.IsLayout || btr.IsAnonymous || btr.IsFromExternalReference) continue;
            _blocks.Add((btr.Name, id));
        }
    }

    /// <summary>
    /// Whether this part has a block in the client's library, without drawing anything.
    /// The sheet has to know what it will actually draw before it can decide whether the
    /// piece and its annotation fit: counting parts that turn out to be undrawable inflated
    /// the estimate and rescaled a viewport that did not need rescaling.
    /// </summary>
    public bool CanResolve(string partNumber) => !Resolve(partNumber).IsNull;

    public bool TryInsert(string partNumber, string face, Point3d position, out BlockReference? blockRef)
    {
        blockRef = null;
        var blockId = Resolve(partNumber);
        if (blockId.IsNull) return false;

        blockRef = new BlockReference(position, blockId);
        TrySetVisibility(blockRef, partNumber, face);
        return true;
    }

    private ObjectId Resolve(string partNumber)
    {
        if (_byPart.TryGetValue(partNumber, out var cached)) return cached;

        var digits = Regex.Match(partNumber, @"\d+").Value;
        var letters = Regex.Match(partNumber, @"^[A-Za-z]+").Value;
        var prefix2 = letters.Length >= 2 ? letters[..2] : letters;

        // Exact name, then prefix+digits (INS087 -> IN087), then any block carrying the digits.
        var match =
            _blocks.FirstOrDefault(b => b.Name.Equals(partNumber, StringComparison.OrdinalIgnoreCase)).Id;

        if (match.IsNull && digits.Length > 0)
            match = _blocks.FirstOrDefault(b =>
                b.Name.StartsWith(prefix2, StringComparison.OrdinalIgnoreCase) &&
                b.Name.Contains(digits, StringComparison.OrdinalIgnoreCase)).Id;

        if (match.IsNull && digits.Length > 0)
            match = _blocks.FirstOrDefault(b =>
                b.Name.Contains(partNumber, StringComparison.OrdinalIgnoreCase)).Id;

        if (match.IsNull && _reported.Add(partNumber))
            _log.Warn($"No block in the seed library matches part {partNumber}; it is not drawn and is listed in the derived note.");

        _byPart[partNumber] = match;
        return match;
    }

    /// <summary>
    /// Sets the visibility state whose name mentions the face we are drawing. The state
    /// names are the client's (IND087_TIF, PAG018_SIF), so we match on the face token
    /// rather than assuming a naming scheme.
    /// </summary>
    private void TrySetVisibility(BlockReference blockRef, string partNumber, string face)
    {
        if (string.IsNullOrWhiteSpace(face)) return;

        try
        {
            if (!blockRef.IsDynamicBlock) return;

            foreach (DynamicBlockReferenceProperty property in blockRef.DynamicBlockReferencePropertyCollection)
            {
                if (!property.PropertyName.StartsWith("Visibility", StringComparison.OrdinalIgnoreCase)) continue;

                var allowed = property.GetAllowedValues()
                    .Select(v => Convert.ToString(v) ?? "")
                    .ToList();

                var wanted = allowed.FirstOrDefault(v => v.EndsWith("_" + face, StringComparison.OrdinalIgnoreCase))
                             ?? allowed.FirstOrDefault(v => v.Contains(face, StringComparison.OrdinalIgnoreCase));

                if (wanted == null) continue;
                property.Value = wanted;
                return;
            }
        }
        catch (Exception ex)
        {
            if (_reported.Add(partNumber + ":vis"))
                _log.Warn($"Could not set the visibility state on {partNumber}: {ex.Message}");
        }
    }
}
