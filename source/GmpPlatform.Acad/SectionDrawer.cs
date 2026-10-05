using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using GmpPlatform.Core.Tickets;

namespace GmpPlatform.Acad;

/// <summary>
/// Cross-sections for sheet 3, and the cut markers on the TIF view that point at them.
/// <para>
/// Built to the recipe measured from the approved drawing rather than to a generic idea of
/// a section: the insulation is ANSI37 at scale 5 on layer INSULATION HATCH, titles are
/// "SECTION &quot;A&quot;" in the Tindall style at width factor 0.725 and colour 4, and cut
/// markers are the client's own dynamic SecCut_w_PlanDetail_TAG block carrying SEC#.
/// </para>
/// <para>
/// Where to cut is a rule GMP has not given us. The rule used here — one section through
/// each distinct embedded part, because that is what most of the approved sections show —
/// is marked as derived on the drawing.
/// </para>
/// </summary>
internal sealed class SectionDrawer
{
    private const string LayerPanel = "PANEL";
    private const string LayerInsulationHatch = "INSULATION HATCH";
    private const string LayerText = "TEXT";
    private const string LayerTags = "TAGS";
    private const string LayerDims = "DIMENSIONS";
    private const string MarkerBlock = "SecCut_w_PlanDetail_TAG";
    private const double SliceWidthIn = 30.0;   // length of panel shown either side of the cut

    private readonly Database _db;
    private readonly Transaction _tr;
    private readonly PieceTicket _ticket;
    private readonly DrawLog _log;
    private readonly Func<ObjectId> _textStyle;
    private readonly Func<ObjectId> _dimStyle;
    private readonly Action<Entity> _append;
    private readonly Func<string, short, string> _layer;

    public SectionDrawer(
        Database db, Transaction tr, PieceTicket ticket, DrawLog log,
        Func<ObjectId> textStyle, Func<ObjectId> dimStyle,
        Action<Entity> append, Func<string, short, string> layer)
    {
        _db = db;
        _tr = tr;
        _ticket = ticket;
        _log = log;
        _textStyle = textStyle;
        _dimStyle = dimStyle;
        _append = append;
        _layer = layer;
    }

    /// <summary>
    /// One planned cut: the part it is named after, where it is, its letter, and any other
    /// parts at the same spot (an insert and the hardware assembled with it).
    /// </summary>
    public sealed record Cut(string Letter, TicketPart Part, PartInstance At, IReadOnlyList<string> With);

    /// <summary>
    /// One section per distinct location that carries an embedded part, lettered so the
    /// letters read left to right on the TIF view (<see cref="SheetConventions.InSheetReadingOrder{T}"/>).
    /// <para>
    /// Parts sharing a location are one section, not several. On AFIP-1035 the EMS016,
    /// EMS018 and EMS325 items sit exactly where each INS087 does — they are its assembly
    /// hardware — so cutting a section per part type produced three identical sections.
    /// </para>
    /// </summary>
    public List<Cut> PlanCuts(int available)
    {
        // First instance of each part type is its representative location.
        var reps = _ticket.Parts
            .Where(p => p.Instances.Count > 0)
            .Select(p => (Part: p, At: p.Instances.OrderBy(i => i.X).First()))
            .ToList();

        // Group representatives that sit at the same place (within 1").
        var groups = reps
            .GroupBy(r => (X: Math.Round(r.At.X), Y: Math.Round(r.At.Y)))
            .Select(g =>
            {
                // Name the section after the part that is not assembly hardware; the EMS
                // items are welded studs and plates that travel with an insert.
                var ordered = g
                    .OrderBy(r => r.Part.PartNumber.StartsWith("EMS", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                    .ThenBy(r => r.Part.PartNumber)
                    .ToList();
                var primary = ordered[0];
                var with = ordered.Skip(1).Select(r => r.Part.PartNumber).ToList();
                return (primary.Part, primary.At, With: (IReadOnlyList<string>)with);
            });

        var lettered = SheetConventions
            .InSheetReadingOrder(groups, c => c.At.X, _ticket.Dimensions.LengthIn)
            .Take(available)
            .ToList();

        return lettered
            .Select((c, i) => new Cut(Letter(i), c.Part, c.At, c.With))
            .ToList();
    }

    private static string Letter(int index) =>
        index < 26 ? ((char)('A' + index)).ToString() : $"A{index - 25}";

    // ------------------------------------------------------------------ sections

    /// <summary>
    /// Clears every section window on the sheet and draws the planned cuts into them.
    /// Windows beyond the cuts are cleared too: leaving them would show the previous
    /// job's sections on this piece's ticket.
    /// </summary>
    public int DrawSections(SheetWindow sheet, IReadOnlyList<Cut> cuts, Func<ViewWindow, int> clear)
    {
        var windows = ReadingOrder(sheet.All);

        var cleared = windows.Sum(clear);
        _log.Info($"    cleared {cleared} object(s) across {windows.Count} section window(s)");

        var drawn = 0;
        for (var i = 0; i < cuts.Count && i < windows.Count; i++)
        {
            DrawSection(windows[i], cuts[i]);
            drawn++;
        }

        if (cuts.Count > windows.Count)
            _log.Warn($"{cuts.Count - windows.Count} planned section(s) had no window on the sheet.");

        return drawn;
    }

    /// <summary>
    /// Section windows in reading order as they appear <b>on the sheet</b>: rows top to
    /// bottom, then left to right, by each viewport's position on paper.
    /// <para>
    /// Rows are found by overlap, not by rounding Y to a fixed grid. The approved sheet's
    /// section viewports in one row differ in height and centre by several inches, and a
    /// fixed grid split a row in two, so the letters came out A, B, D on top and C below.
    /// Ordering by the model-space area each viewport looks at gave the same symptom: the
    /// seed's viewports are not arranged in model space the way they sit on the sheet.
    /// </para>
    /// </summary>
    private static List<ViewWindow> ReadingOrder(IReadOnlyList<ViewWindow> windows)
    {
        var onPaper = windows.All(w => w.PaperHeight > 0);
        double Cx(ViewWindow w) => onPaper ? w.PaperCenterX : w.CenterX;
        double Cy(ViewWindow w) => onPaper ? w.PaperCenterY : w.CenterY;
        double HalfHeight(ViewWindow w) => (onPaper ? w.PaperHeight : w.Height) / 2.0;

        var rows = new List<List<ViewWindow>>();
        foreach (var w in windows.OrderByDescending(Cy))
        {
            // Same row when this window's centre falls inside the vertical span of the
            // first window already placed in that row.
            var row = rows.FirstOrDefault(r =>
                Math.Abs(Cy(w) - Cy(r[0])) <= HalfHeight(r[0]));
            if (row == null) rows.Add(row = new List<ViewWindow>());
            row.Add(w);
        }

        return rows.SelectMany(r => r.OrderBy(Cx)).ToList();
    }

    private void DrawSection(ViewWindow window, Cut cut)
    {
        var d = _ticket.Dimensions;
        var scale = window.AnnotationScale > 0 ? window.AnnotationScale : 19.2;
        var thickness = d.ThicknessIn > 0 ? d.ThicknessIn : 9.0;
        var outer = d.WytheOuterIn > 0 ? d.WytheOuterIn : 3.0;
        var inner = d.WytheInnerIn > 0 ? d.WytheInnerIn : 3.0;

        // Panel slice centred in the window; the TIF face is on top, as the approved
        // sections are drawn, with the BIF arrow pointing at the bottom face.
        var left = window.CenterX - SliceWidthIn / 2.0;
        var bottom = window.CenterY - thickness / 2.0;
        var top = bottom + thickness;
        var right = left + SliceWidthIn;

        Rect(left, bottom, right, top, LayerPanel);

        // Wythe boundaries: outer wythe at the BIF face, inner wythe at the TIF face.
        var insulationBottom = bottom + outer;
        var insulationTop = top - inner;
        Line(left, insulationBottom, right, insulationBottom, LayerPanel);
        Line(left, insulationTop, right, insulationTop, LayerPanel);
        InsulationHatch(left, insulationBottom, right, insulationTop);

        // The embedded part, at the depth measured from the TIF face.
        var depth = Math.Abs(cut.At.DepthIn);
        var isTif = cut.At.Face.Equals("TIF", StringComparison.OrdinalIgnoreCase);
        var partTop = isTif ? top : bottom + Math.Max(depth, 0.5);
        var partBottom = isTif ? top - Math.Max(depth, 0.5) : bottom;
        var partHalf = 1.5;
        Rect(window.CenterX - partHalf, partBottom, window.CenterX + partHalf, partTop, LayerPanel);

        var textHeight = 2.4 * scale / 19.2;

        // Part label with leader, in the client's wording. It goes up and to the LEFT:
        // the wythe dimension stack occupies the right-hand side, and a label there
        // collides with it as soon as it runs to a second line.
        var labelPoint = new Point3d(window.CenterX - 14.0, top + 7.0, 0);
        var leader = new Autodesk.AutoCAD.DatabaseServices.Line(
            new Point3d(window.CenterX, (partTop + partBottom) / 2.0, 0), labelPoint)
        {
            Layer = _layer(LayerText, 7)
        };
        _append(leader);

        // Assembly hardware is named after the insert, the way the ticket writes
        // "(2) RSX05480 SPLICED W/(2) RSX05097".
        // Hardware is stacked two to a line: section viewports are narrow, and a single
        // long line is clipped by the viewport edge.
        var label = $"{cut.Part.PartNumber} ({cut.At.Face})";
        if (cut.With.Count > 0)
        {
            var lines = cut.With
                .Select((p, i) => (p, i))
                .GroupBy(x => x.i / 2, x => x.p)
                .Select(g => string.Join(", ", g));
            label += "\\PW/ " + string.Join("\\P", lines);
        }
        TextRightAligned(label, labelPoint.X - 1.0, labelPoint.Y, textHeight);

        // Wythe stack dimensions on the right, as on every approved section.
        StackDimension(right + 6.0, bottom, insulationBottom, scale);
        StackDimension(right + 6.0, insulationBottom, insulationTop, scale);
        StackDimension(right + 6.0, insulationTop, top, scale);
        StackDimension(right + 14.0, bottom, top, scale);

        // Recess depth, where the part sits below the face.
        if (depth > 0.05)
            StackDimension(window.CenterX - 8.0, partBottom, partTop, scale);

        // BIF indicator, then the title, underlined, as the ticket writes it. Kept tight to
        // the dimension stack: further out it falls past the narrower section viewports.
        Text("BIF", right + 17.0, bottom + thickness / 2.0, textHeight, 7);
        Title($"SECTION \"{cut.Letter}\"", window.CenterX, bottom - 10.0, textHeight * 1.4);
    }

    private void StackDimension(double x, double y1, double y2, double scale)
    {
        var dim = new RotatedDimension
        {
            XLine1Point = new Point3d(x - 3.0, y1, 0),
            XLine2Point = new Point3d(x - 3.0, y2, 0),
            DimLinePoint = new Point3d(x, (y1 + y2) / 2.0, 0),
            Rotation = Math.PI / 2,
            DimensionStyle = _dimStyle(),
            Layer = _layer(LayerDims, 7)
        };
        DimensionPresets.TightFit.ApplyTo(dim, scale);
        _append(dim);
    }

    private void InsulationHatch(double x0, double y0, double x1, double y1)
    {
        // Boundary first: a hatch needs an object in the database to loop around.
        var boundary = new Polyline(4) { Layer = _layer(LayerInsulationHatch, 7), Closed = true };
        boundary.AddVertexAt(0, new Point2d(x0, y0), 0, 0, 0);
        boundary.AddVertexAt(1, new Point2d(x1, y0), 0, 0, 0);
        boundary.AddVertexAt(2, new Point2d(x1, y1), 0, 0, 0);
        boundary.AddVertexAt(3, new Point2d(x0, y1), 0, 0, 0);
        _append(boundary);

        try
        {
            var hatch = new Hatch { Layer = _layer(LayerInsulationHatch, 7) };
            _append(hatch);
            hatch.PatternScale = 5.0;
            hatch.SetHatchPattern(HatchPatternType.PreDefined, "ANSI37");
            hatch.Associative = false;
            hatch.AppendLoop(HatchLoopTypes.Outermost, new ObjectIdCollection { boundary.ObjectId });
            hatch.EvaluateHatch(true);
        }
        catch (Exception ex)
        {
            _log.Warn($"Insulation hatch could not be built ({ex.Message}); boundary left unhatched.");
        }
    }

    // ------------------------------------------------------------------ cut markers

    /// <summary>
    /// Places the client's section marker on the TIF view at each cut, carrying the same
    /// letter as the section it points to — the link that makes a section readable.
    /// </summary>
    /// <param name="tifOrigin">The origin the TIF piece was drawn from, so markers land on
    /// the parts they cut.</param>
    public int DrawCutMarkers(Point3d tifOrigin, IReadOnlyList<Cut> cuts)
    {
        var bt = (BlockTable)_tr.GetObject(_db.BlockTableId, OpenMode.ForRead);
        if (!bt.Has(MarkerBlock))
        {
            _log.Warn($"Block '{MarkerBlock}' not in the seed; section cuts were not marked on the TIF view.");
            return 0;
        }

        var blockId = bt[MarkerBlock];
        var markerScale = ExistingMarkerScale(blockId);
        var placed = 0;

        foreach (var cut in cuts)
        {
            // Marker sits just outside the top edge of the piece, over the cut.
            var at = new Point3d(
                SheetConventions.StationToDrawingX(tifOrigin.X, _ticket.Dimensions.LengthIn, cut.At.X),
                tifOrigin.Y + _ticket.Dimensions.HeightIn + 4.0,
                0);

            var marker = new BlockReference(at, blockId)
            {
                ScaleFactors = new Scale3d(markerScale),
                Layer = _layer(LayerTags, 7)
            };
            _append(marker);
            AddAttributes(marker, blockId, new Dictionary<string, string> { ["SEC#"] = cut.Letter });
            placed++;
        }

        return placed;
    }

    /// <summary>Uses the scale the drafter already inserted this marker at, when there is one.</summary>
    private double ExistingMarkerScale(ObjectId blockId)
    {
        try
        {
            var btr = (BlockTableRecord)_tr.GetObject(blockId, OpenMode.ForRead);
            foreach (ObjectId refId in btr.GetBlockReferenceIds(true, false))
            {
                if (_tr.GetObject(refId, OpenMode.ForRead) is BlockReference br && !br.IsErased)
                    return br.ScaleFactors.X;
            }

            // Dynamic blocks are referenced through anonymous definitions.
            foreach (ObjectId anonId in btr.GetAnonymousBlockIds())
            {
                var anon = (BlockTableRecord)_tr.GetObject(anonId, OpenMode.ForRead);
                foreach (ObjectId refId in anon.GetBlockReferenceIds(true, false))
                {
                    if (_tr.GetObject(refId, OpenMode.ForRead) is BlockReference br && !br.IsErased)
                        return br.ScaleFactors.X;
                }
            }
        }
        catch { /* fall through to the default */ }

        return 13.127; // measured from the approved drawing
    }

    private void AddAttributes(BlockReference blockRef, ObjectId blockId, IDictionary<string, string> values)
    {
        var btr = (BlockTableRecord)_tr.GetObject(blockId, OpenMode.ForRead);
        if (!btr.HasAttributeDefinitions) return;

        foreach (ObjectId id in btr)
        {
            if (_tr.GetObject(id, OpenMode.ForRead) is not AttributeDefinition def || def.Constant) continue;

            var att = new AttributeReference();
            att.SetAttributeFromBlock(def, blockRef.BlockTransform);
            if (values.TryGetValue(def.Tag, out var v)) att.TextString = v;

            blockRef.AttributeCollection.AppendAttribute(att);
            _tr.AddNewlyCreatedDBObject(att, true);
        }
    }

    // ------------------------------------------------------------------ primitives

    private void Rect(double x0, double y0, double x1, double y1, string layer)
    {
        var p = new Polyline(4) { Layer = _layer(layer, 7), Closed = true };
        p.AddVertexAt(0, new Point2d(x0, y0), 0, 0, 0);
        p.AddVertexAt(1, new Point2d(x1, y0), 0, 0, 0);
        p.AddVertexAt(2, new Point2d(x1, y1), 0, 0, 0);
        p.AddVertexAt(3, new Point2d(x0, y1), 0, 0, 0);
        _append(p);
    }

    private void Line(double x0, double y0, double x1, double y1, string layer) =>
        _append(new Autodesk.AutoCAD.DatabaseServices.Line(new Point3d(x0, y0, 0), new Point3d(x1, y1, 0))
        {
            Layer = _layer(layer, 7)
        });

    private void TextRightAligned(string contents, double x, double y, double height) =>
        _append(new MText
        {
            Contents = contents,
            Location = new Point3d(x, y, 0),
            Attachment = AttachmentPoint.BottomRight,
            TextHeight = height,
            Layer = _layer(LayerText, 7),
            TextStyleId = _textStyle()
        });

    private void Text(string contents, double x, double y, double height, short color) =>
        _append(new MText
        {
            Contents = contents,
            Location = new Point3d(x, y, 0),
            TextHeight = height,
            Layer = _layer(LayerText, color),
            TextStyleId = _textStyle()
        });

    /// <summary>Section title in the client's own formatting codes.</summary>
    private void Title(string contents, double x, double y, double height) =>
        _append(new MText
        {
            Contents = $"\\A1;{{\\W0.725;\\C4;\\L{contents}}}",
            Location = new Point3d(x, y, 0),
            Attachment = AttachmentPoint.TopCenter,
            TextHeight = height,
            Layer = _layer(LayerText, 7),
            TextStyleId = _textStyle()
        });
}
