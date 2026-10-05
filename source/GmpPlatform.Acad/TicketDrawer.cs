using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using GmpPlatform.Core.Rebar;
using GmpPlatform.Core.Settings;
using GmpPlatform.Core.Tickets;

namespace GmpPlatform.Acad;

/// <summary>
/// Draws one piece ticket into the client's own drawing.
/// <para>
/// The seed is a copy of GMP's approved DWG, so every layer, text style, dimension style,
/// block definition, layout and page setup is already correct and is reused rather than
/// recreated. This class clears the piece drawing out of the main view windows and draws
/// the new piece in the same model-space coordinates the existing viewports already look
/// at, which is what makes the generated sheets compose like theirs.
/// </para>
/// </summary>
internal sealed class TicketDrawer
{
    // Names come from the client's standards profile (built-in default: the Tindall seed).
    private string LayerPanel => _standards.Layers.Panel;
    private string LayerDims => _standards.Layers.Dimensions;
    private string LayerText => _standards.Layers.Text;
    private string LayerInsert => _standards.Layers.Insert;
    private string LayerInsulation => _standards.Layers.Insulation;
    private string LayerInsulationHatch => _standards.Layers.InsulationHatch;
    private string LayerDerived => _standards.Layers.Derived;
    private string DimStyleName => _standards.Styles.Dimension;
    private string TextStyleName => _standards.Styles.Text;

    private readonly StandardsProfile _standards;
    private readonly Database _db;
    private readonly Transaction _tr;
    private readonly PieceTicket _ticket;
    private readonly DrawLog _log;
    private readonly PlatformSettings _settings;
    private readonly RebarRules _rebarRules = new();
    private readonly IReadOnlyList<RebarGroup> _rebar;

    /// <summary>Inner loops and top/bottom-edge cut-outs, numbered B.O.-1, B.O.-2, ...</summary>
    private readonly IReadOnlyList<PlannedOpening> _openings;

    /// <summary>Openings sharing start and stop stations, dimensioned together.</summary>
    private readonly IReadOnlyList<OpeningGroup> _openingGroups;

    /// <summary>One resolver for the whole drawing, so each missing block is reported once.</summary>
    private readonly PartBlockResolver _resolver;

    /// <summary>Hardware parts (EMS studs and plates on an insert), each with its host part.</summary>
    private readonly IReadOnlyDictionary<string, string> _hardware;

    /// <summary>Station callouts drawn for the view currently being built.</summary>
    private int _stationCallouts;

    /// <summary>Dimension chains stacked below the piece in the view currently being built.</summary>
    private int _belowChains;

    /// <summary>
    /// How far the SIF view and the notes under it move down to clear the chains below the
    /// piece. <see cref="SheetConventions.SifOffset"/> was measured on a sheet with one chain
    /// below, so one costs nothing and each further chain takes one band.
    /// </summary>
    private double SifDrop => Math.Max(0, _belowChains - 1) * ChainBandStep + _belowExtra;

    /// <summary>Room below the piece beyond whole bands (the opening width row's extra half).</summary>
    private double _belowExtra;

    /// <summary>
    /// Height above the piece's base for the overall length string: the next free band beyond
    /// every chain above the piece, which is where the approved ticket runs its 46'-3".
    /// </summary>
    private double _overallAbove;

    /// <summary>
    /// Where the TIF piece was actually drawn: after the window was fitted to the piece and the
    /// piece shifted off-centre. The section cut markers must use this, not a recomputation.
    /// </summary>
    private Point3d? _tifOrigin;

    /// <summary>
    /// Stations that already carry a callout in the current view, and how many.
    /// <para>
    /// All callouts share one band against the piece, so two parts at the same station would
    /// print on top of each other. AFIP-1035 has no such pair, which is exactly why this needs
    /// handling rather than ignoring: it would first appear on some other mark, as unreadable
    /// overstruck text rather than as an error.
    /// </para>
    /// </summary>
    private readonly Dictionary<(bool Below, long X), int> _calloutRows = new();

    /// <summary>
    /// Turns a panel station — the contract's X, zero at the START PANEL end — into a drawing
    /// coordinate. Stations run <b>right to left</b> across the sheet, which is what the
    /// approved ticket's "◄— DIRECTION OF STUB DIMS." states.
    /// <para>
    /// This was measured, not assumed. AFIP-1035's four INS087 recesses are at stations 30,
    /// 171, 342 and 498 — the same values the approved drawing dimensions, so both measure
    /// from the same physical end — yet their callouts sit at drawing x 521.5, 380.5, 209.5
    /// and 53.5. Fitting <c>x = C - station</c> reproduces all four exactly; fitting
    /// <c>x = C + station</c> is 15" out on the middle two. Drawing stations left to right, as
    /// this engine did, produced a mirror image of the client's sheet: every value correct and
    /// the piece the wrong way round, which on a shop ticket is how a panel gets cast
    /// backwards.
    /// </para>
    /// <para>
    /// Read off one approved ticket, so it is a convention to confirm for other marks rather
    /// than a law. It is expressed once, here, so confirming it is a one-line change.
    /// </para>
    /// </summary>
    private double StationX(Point3d origin, double station) =>
        SheetConventions.StationToDrawingX(origin.X, _ticket.Dimensions.LengthIn, station);

    public TicketDrawer(Database db, Transaction tr, PieceTicket ticket, DrawLog log,
        PlatformSettings settings, StandardsProfile standards)
    {
        _db = db;
        _tr = tr;
        _ticket = ticket;
        _log = log;
        _settings = settings;
        _standards = standards;

        // The model carries no reinforcement for wall panels. Rule-generated field bars are
        // Phase 2 work and off unless DrawGeneratedRebar is set; in Phase 1 the drafter adds
        // reinforcement by hand. If a future model does carry rebar, the exporter should
        // supply it and generation becomes a fallback.
        var modelled = ticket.Parts.Any(p => p.PartNumber.StartsWith("RSX", StringComparison.OrdinalIgnoreCase));
        _rebar = !settings.DrawGeneratedRebar || modelled
            ? Array.Empty<RebarGroup>()
            : RebarPlanner.Plan(ticket.Dimensions, _rebarRules);
        _log.Info(settings.DrawGeneratedRebar
            ? $"generated reinforcement: on ({_rebar.Count} group(s))"
            : "generated reinforcement: off (DrawGeneratedRebar); reinforcement is added by the drafter");

        _resolver = new PartBlockResolver(db, tr, log);
        _hardware = AssemblyHardware.Find(ticket.Parts);
        if (_hardware.Count > 0)
            _log.Info($"assembly hardware (no BOM row): {string.Join(", ", _hardware.Select(h => $"{h.Key} on {h.Value}"))}");

        _openings = OpeningPlan.Find(ticket.Geometry);
        _openingGroups = OpeningPlan.Group(_openings, ticket.Dimensions.HeightIn);
        if (_openings.Count > 0)
        {
            _log.Info($"openings: {string.Join("; ", _openings.Select(o =>
                $"{OpeningLabel(o.Number.ToString())} {o.Kind} {o.Width:0.###} x {o.Height:0.###} at station {o.MinX:0.###}, y {o.MinY:0.###}"))}");
        }
    }

    private string OpeningLabel(string numbers) => _standards.Labels.OpeningPrefix + numbers;

    public void Draw()
    {
        EnsureLayer(LayerDerived, 1); // red: anything we computed rather than read

        var windows = SheetWindows.Discover(_db, _tr);
        _log.Info($"layouts: {string.Join(", ", windows.Select(w => $"{w.Layout} ({w.Viewports} vp)"))}");

        // Sheet roles: layout names from settings, otherwise the seed's tab order - on the
        // approved ticket "1 of 3" is the top-in-form view, "2 of 3" the bottom-in-form finish
        // layout, "3 of 3" the sections. Roles used to be inferred from whether a window could
        // hold the piece, which broke as soon as viewports were rescaled to fit - and silently
        // drew nothing for any piece larger than the seed's layout.
        var roles = SheetRoles.Assign(
            windows.Select(w => new SheetCandidate(w.Layout, w.TabOrder, w.Main != null, w.All.Count)),
            _settings.TifLayoutName, _settings.BifLayoutName, _settings.SectionsLayoutName);
        foreach (var warning in roles.Warnings) _log.Warn(warning);

        SheetWindow? Role(string? layout) => windows.FirstOrDefault(w => w.Layout == layout);
        var tif = Role(roles.Tif);
        var bif = Role(roles.Bif);
        var sections = Role(roles.Sections);
        _log.Info($"sheet roles: TIF={roles.Tif ?? "-"}, BIF={roles.Bif ?? "-"}, sections={roles.Sections ?? "-"}");

        if (tif == null)
        {
            _log.Warn("No layout with a drawing viewport; nothing was drawn.");
            return;
        }

        DrawPieceView(tif, "TIF", drawNotes: true);
        if (bif != null) DrawPieceView(bif, "BIF", drawNotes: false);

        DrawSectionsSheet(sections);

        var title = FillTitleBlocks();
        var bom = FillBillOfMaterial();
        _log.Info($"filled {title} title-block field(s) and {bom} BOM row(s)");
    }

    /// <summary>
    /// Draws one face of the piece into the window a sheet already frames. Parts are
    /// filtered to that face, the way the approved ticket separates the TIF and BIF views.
    /// </summary>
    private void DrawPieceView(SheetWindow sheet, string face, bool drawNotes)
    {
        // Decide what is drawable before fitting, so the space the sheet reserves and the
        // content it ends up with are the same set. Assembly hardware (EMS parts that only ever
        // sit on another part) gets no symbol, callout or BOM row, as on the approved ticket,
        // and neither does a part with no block in the client library. Both are named in the
        // derived note instead.
        var parts = PartsOnFace(face);
        var drawable = parts.Where(p => !_hardware.ContainsKey(p.PartNumber) && _resolver.CanResolve(p.PartNumber)).ToList();

        var isTif = face.Equals("TIF", StringComparison.OrdinalIgnoreCase);
        var window = FitToPiece(sheet.Main!.Value, drawable, annotateOpenings: isTif);

        // The piece is not centred in its window: the approved sheet keeps a band clear on the
        // right for the stub-dimension column that reads down the side of the piece, so the
        // piece sits left of centre. Centring it put our content half a column out of step
        // with theirs on every overlay. Vertically the sheet is composed from the top down
        // (SheetConventions.TopMarginPaper).
        var annotationScale = window.AnnotationScale > 0 ? window.AnnotationScale : 64.0;
        var origin = new Point3d(
            SheetConventions.PieceOriginX(window.CenterX, _ticket.Dimensions.LengthIn),
            SheetConventions.PieceOriginY(
                window.CenterY + window.Height / 2.0, annotationScale, _ticket.Dimensions.HeightIn,
                AnnotationAbove(drawable, isTif, annotationScale)),
            0);

        if (face.Equals("TIF", StringComparison.OrdinalIgnoreCase)) _tifOrigin = origin;

        _log.Info($"--- {sheet.Layout}: {face} view, window centre ({window.CenterX:N1}, " +
                  $"{window.CenterY:N1}) size {window.Width:N0} x {window.Height:N0}, " +
                  $"scale {annotationScale:N0}, origin ({origin.X:N1}, {origin.Y:N1})");

        var cleared = ClearWindow(window);
        _log.Info($"    cleared {cleared} existing object(s)");

        DrawOutline(origin);
        DrawInsulationExtent(origin, annotationScale);

        // Field reinforcement is drawn once, on the TIF view where the approved ticket
        // shows it; both wythes' bars project onto the same lines in plan.
        if (face.Equals("TIF", StringComparison.OrdinalIgnoreCase))
            DrawRebar(origin, annotationScale);

        var placed = DrawParts(origin, drawable);

        _stationCallouts = 0;
        _belowChains = 0;
        _belowExtra = 0;
        _calloutRows.Clear();   // collision tracking is per view
        var chains = DrawStubChains(origin, annotationScale, drawable, annotateOpenings: isTif);
        var callouts = _stationCallouts;
        if (isTif) DrawOpeningColumns(origin, annotationScale);
        if (face.Equals("TIF", StringComparison.OrdinalIgnoreCase)) DrawSifView(origin, annotationScale, parts);
        DrawOverallDimensions(origin, annotationScale);
        DrawViewTitle(origin, annotationScale, face);
        DrawEndLabels(origin, window, annotationScale);
        if (drawNotes) DrawDerivedNote(origin, annotationScale);

        _log.Info($"    {face}: {drawable.Count} of {parts.Count} part type(s) drawn, " +
                  $"{placed} symbol(s), {chains} chain(s), {callouts} callout(s)");
    }

    /// <summary>Scales a drafter would actually pick, smallest reduction first.</summary>
    private static readonly double[] StandardScales = { 16, 24, 32, 48, 64, 96, 128, 192, 256 };

    // Layout of the bands around the piece, in model inches. Kept together because the
    // fit calculation and the drawing have to agree: if they drift apart the piece either
    // overflows its viewport or triggers a needless rescale.
    // The layout constants and the station convention live in GmpPlatform.Core.Tickets.
    // SheetConventions: they describe the client's drafting convention rather than anything
    // about AutoCAD, and keeping them in the contract project puts them within reach of tests.
    private const double ChainBandBase = SheetConventions.ChainBandBase;
    private const double ChainBandStep = SheetConventions.ChainBandStep;
    private const double CalloutBandGap = SheetConventions.CalloutBandGap;
    private const double SifOffset = SheetConventions.SifOffset;
    private const double SifDimOffset = SheetConventions.SifDimOffset;
    private const double EdgeBandTolerance = SheetConventions.EdgeBandTolerance;
    private const double RightColumnReserve = SheetConventions.RightColumnReserve;
    private const double CalloutStationOffset = SheetConventions.CalloutStationOffset;

    /// <summary>
    /// Makes the sheet's viewport show the whole piece and its annotation, rescaling it when
    /// the seed's scale is too tight.
    /// <para>
    /// Without this the engine only worked for pieces the seed drawing happened to be laid
    /// out for: a longer panel simply failed the "does it fit" test and nothing was drawn.
    /// Composing the sheet to the piece is what a drafter does, and it is what lets the same
    /// template serve marks of different sizes.
    /// </para>
    /// </summary>
    private ViewWindow FitToPiece(ViewWindow window, IReadOnlyList<TicketPart> drawable, bool annotateOpenings)
    {
        var d = _ticket.Dimensions;

        // Room for what gets drawn around the piece. Chains stack off whichever edge their
        // items sit on, so both directions are counted the same way the drawing allocates
        // them — a fit estimate that assumes everything stacks upward either wastes space
        // above or overruns the SIF view below.
        var chainsBelow = ChainBands(drawable, annotateOpenings, below: true);
        var chainsAbove = ChainBands(drawable, annotateOpenings, below: false);

        var calloutRun = SheetConventions.CalloutRun(window.AnnotationScale > 0 ? window.AnnotationScale : 64.0);
        var above = CalloutBandGap + calloutRun + ChainBandBase + chainsAbove * ChainBandStep + 30.0;
        var below = Math.Max(
            SifOffset + 60.0,                                   // edge view, its dims, notes
            CalloutBandGap + calloutRun + ChainBandBase + chainsBelow * ChainBandStep + SifOffset);
        var requiredWidth = d.LengthIn * 1.12 + RightColumnReserve;
        var requiredHeight = d.HeightIn + above + below;

        if (window.ViewportId.IsNull || window.PaperHeight <= 0 || window.PaperWidth <= 0)
        {
            if (window.Width < requiredWidth || window.Height < requiredHeight)
                _log.Warn("The piece does not fit the viewport and the viewport could not be rescaled.");
            return window;
        }

        // Keep the seed's scale whenever the piece sits sensibly in it: the client's sheets 1
        // and 2 are both 3/16" = 1'-0", and rescaling one of them just because the piece
        // could be drawn larger would break that. Only step in when the piece does not fit,
        // or is so small it would be lost in a window sized for a 46-foot panel.
        var fits = window.Width >= requiredWidth && window.Height >= requiredHeight;
        var tiny = requiredWidth < window.Width * 0.25;
        if (fits && !tiny) return window;

        var needed = Math.Max(requiredWidth / window.PaperWidth, requiredHeight / window.PaperHeight);
        var scale = StandardScales.FirstOrDefault(s => s >= needed);
        if (scale == 0) scale = StandardScales[^1];

        if (window.AnnotationScale > 0 && Math.Abs(window.AnnotationScale - scale) < 0.5) return window;

        try
        {
            var vp = (Viewport)_tr.GetObject(window.ViewportId, OpenMode.ForWrite);
            vp.CustomScale = 1.0 / scale;
            vp.ViewHeight = window.PaperHeight * scale;
            vp.ViewCenter = new Point2d(
                window.CenterX - vp.ViewTarget.X,
                window.CenterY - vp.ViewTarget.Y);

            var fitted = window with
            {
                Width = window.PaperWidth * scale,
                Height = window.PaperHeight * scale,
                Scale = 1.0 / scale
            };

            _log.Info($"    viewport rescaled to 1:{scale:N0} so the piece fits " +
                      $"(needed {requiredWidth:N0} x {requiredHeight:N0}, had {window.Width:N0} x {window.Height:N0})");
            return fitted;
        }
        catch (Exception ex)
        {
            _log.Warn($"Could not rescale the viewport ({ex.Message}); drawing at the seed's scale.");
            return window;
        }

    }

    /// <summary>
    /// The SIF edge view: the piece seen through its thickness, below the main view, as the
    /// approved sheet draws it. Shows the wythe build-up, the insulation extent and each
    /// contained part at its measured depth — all of which the ticket already carries, so
    /// none of it is invented.
    /// </summary>
    private void DrawSifView(Point3d origin, double annotationScale, IReadOnlyList<TicketPart> parts)
    {
        var d = _ticket.Dimensions;
        var thickness = d.ThicknessIn > 0 ? d.ThicknessIn : 9.0;
        var outer = d.WytheOuterIn > 0 ? d.WytheOuterIn : thickness / 3.0;
        var inner = d.WytheInnerIn > 0 ? d.WytheInnerIn : thickness / 3.0;

        var bottom = origin.Y - SifOffset - SifDrop;
        var top = bottom + thickness;
        var left = origin.X;
        var right = origin.X + d.LengthIn;

        // Body and wythe boundaries. The TIF face is uppermost, matching the sections.
        AddRect(left, bottom, right, top, LayerPanel);
        AddLine(left, bottom + outer, right, bottom + outer, LayerPanel);
        AddLine(left, top - inner, right, top - inner, LayerPanel);
        AddInsulationHatch(left, bottom + outer, right, top - inner, annotationScale);

        // Insulation stops short of the panel end; the rest is solid.
        if (d.InsulationLengthIn > 0 && d.InsulationLengthIn < d.LengthIn)
        {
            var x = left + d.InsulationLengthIn;
            AddLine(x, bottom, x, top, LayerInsulation);
        }

        // Each part at its depth, so the edge view shows which face it belongs to.
        var textHeight = 3.0 * annotationScale / 64.0;
        foreach (var part in parts)
        {
            foreach (var instance in part.Instances)
            {
                var depth = Math.Abs(instance.DepthIn);
                var y = instance.Face.Equals("BIF", StringComparison.OrdinalIgnoreCase)
                    ? bottom + Math.Min(depth, thickness)
                    : top - Math.Min(depth, thickness);

                AddLine(StationX(origin, instance.X), y - 1.0, StationX(origin, instance.X), y + 1.0, LayerInsert);
            }
        }

        // Overall length under the edge view, the way the sheet dimensions it.
        var dim = new RotatedDimension
        {
            XLine1Point = new Point3d(left, bottom, 0),
            XLine2Point = new Point3d(right, bottom, 0),
            DimLinePoint = new Point3d((left + right) / 2.0, bottom - SifDimOffset, 0),
            Rotation = 0,
            DimensionStyle = DimStyleId(),
            Layer = EnsureLayer(LayerDims, 7)
        };
        DimensionPresets.Overall.ApplyTo(dim, annotationScale);
        Append(dim);

        Append(new MText
        {
            Contents = _standards.Labels.SifView,
            Location = new Point3d((left + right) / 2.0, bottom - SifDimOffset - 14.0, 0),
            Attachment = AttachmentPoint.TopCenter,
            TextHeight = textHeight * 1.3,
            Layer = EnsureLayer(LayerText, 7),
            TextStyleId = TextStyleId()
        });
    }

    private void AddRect(double x0, double y0, double x1, double y1, string layer)
    {
        var p = new Polyline(4) { Layer = EnsureLayer(layer, 7), Closed = true };
        p.AddVertexAt(0, new Point2d(x0, y0), 0, 0, 0);
        p.AddVertexAt(1, new Point2d(x1, y0), 0, 0, 0);
        p.AddVertexAt(2, new Point2d(x1, y1), 0, 0, 0);
        p.AddVertexAt(3, new Point2d(x0, y1), 0, 0, 0);
        Append(p);
    }

    private void AddLine(double x0, double y0, double x1, double y1, string layer) =>
        Append(new Line(new Point3d(x0, y0, 0), new Point3d(x1, y1, 0)) { Layer = EnsureLayer(layer, 7) });

    /// <summary>Insulation in the client's own pattern (Tindall: ANSI37 at scale 5).</summary>
    private void AddInsulationHatch(double x0, double y0, double x1, double y1, double annotationScale)
    {
        if (y1 - y0 <= 0.01) return;

        var boundary = new Polyline(4) { Layer = EnsureLayer(LayerInsulationHatch, 7), Closed = true };
        boundary.AddVertexAt(0, new Point2d(x0, y0), 0, 0, 0);
        boundary.AddVertexAt(1, new Point2d(x1, y0), 0, 0, 0);
        boundary.AddVertexAt(2, new Point2d(x1, y1), 0, 0, 0);
        boundary.AddVertexAt(3, new Point2d(x0, y1), 0, 0, 0);
        Append(boundary);

        try
        {
            var hatch = new Hatch { Layer = EnsureLayer(LayerInsulationHatch, 7) };
            Append(hatch);
            // The client draws this pattern at scale 5 in their 1:19.2 sections. Held at 5 in
            // a 1:64 view the band comes out solid black, so it follows the view scale.
            hatch.PatternScale = _standards.Styles.InsulationHatchScale * annotationScale / 19.2;
            hatch.SetHatchPattern(HatchPatternType.PreDefined, _standards.Styles.InsulationHatchPattern);
            hatch.Associative = false;
            hatch.AppendLoop(HatchLoopTypes.Outermost, new ObjectIdCollection { boundary.ObjectId });
            hatch.EvaluateHatch(true);
        }
        catch (Exception ex)
        {
            _log.Warn($"Edge-view insulation hatch failed ({ex.Message}); boundary left unhatched.");
        }
    }

    /// <summary>
    /// Rule-generated field reinforcement, drawn with the client's own <c>REBAR</c> dynamic
    /// block where the seed provides it and as a dashed line where it does not.
    /// <para>
    /// The block carries the bar in two dynamic properties — a visibility state naming the
    /// size (<c>#3</c>) and <c>Distance1</c> giving the length in inches (<c>143</c>) — which
    /// is precisely what our part numbers already encode: <c>RSX03143</c> is a #3 bar 143"
    /// long. The approved sheet places 31 of these; drawing plain lines instead left the
    /// largest single block gap on the sheet and made rule-generated steel look unlike the
    /// drafter's.
    /// </para>
    /// <para>
    /// The bars remain derived content: they are not in the model, so they stay on the
    /// <c>GMP-DERIVED</c> layer and keep their entry in the derived note.
    /// </para>
    /// </summary>
    private void DrawRebar(Point3d origin, double annotationScale)
    {
        if (_rebar.Count == 0) return;

        var layer = EnsureLayer(LayerDerived, 1);
        var linetype = DashedLinetype();
        var lines = 0;
        var blocks = 0;

        foreach (var group in _rebar)
        {
            foreach (var offset in group.Offsets)
            {
                Point3d a, b;
                if (group.Direction == "longitudinal")
                {
                    // Both ends map through the station convention, so the bar still runs
                    // from its start station to its end station once the sheet is mirrored.
                    a = new Point3d(StationX(origin, group.StartIn), origin.Y + offset, 0);
                    b = new Point3d(StationX(origin, group.StartIn + group.LengthIn), origin.Y + offset, 0);
                }
                else
                {
                    a = new Point3d(StationX(origin, offset), origin.Y + group.StartIn, 0);
                    b = new Point3d(StationX(origin, offset), origin.Y + group.StartIn + group.LengthIn, 0);
                }

                if (TryInsertRebarBlock(group, a, b, layer)) { blocks++; continue; }

                var bar = new Line(a, b) { Layer = layer };
                if (!linetype.IsNull)
                {
                    bar.LinetypeId = linetype;
                    bar.LinetypeScale = annotationScale / 8.0;
                }

                Append(bar);
                lines++;
            }
        }

        // One callout per bar group, worded like the ticket and marked derived.
        var textHeight = 3.5 * annotationScale / 64.0;
        var y = origin.Y - 42.0;
        // A spliced pair gets one callout, on the leading bar, which names its partner.
        bool IsTrailingSplice(RebarGroup g) =>
            g.SplicedWith.Length > 0 &&
            _rebar.Any(o => o.PartNumber == g.SplicedWith && o.StartIn < g.StartIn);

        foreach (var group in _rebar.Where(g => !IsTrailingSplice(g)))
        {
            var perWythe = group.Count / 2;
            var text = group.SplicedWith.Length > 0
                ? $"({perWythe}) {group.PartNumber} SPLICED W/({perWythe}) {group.SplicedWith}\\P(1) TIF & (1) BIF (MIN. LAP {TicketUnits.FeetInches(_rebarRules.LapIn)})"
                : $"({group.Count}) {group.PartNumber} (1) TIF & (1) BIF";

            Append(new MText
            {
                Contents = "{\\C1;DERIVED:} " + text,
                Location = new Point3d(origin.X, y, 0),
                TextHeight = textHeight,
                Layer = layer,
                TextStyleId = TextStyleId()
            });
            y -= textHeight * 3.2;
        }

        _log.Info($"    rebar: {_rebar.Count} group(s), {blocks} REBAR block(s) + {lines} line(s) " +
                  $"({string.Join(", ", _rebar.Select(g => $"{g.PartNumber} x{g.Count}"))})");
    }

    /// <summary>The client's hidden/dashed linetype, if the seed carries one.</summary>
    /// <summary>
    /// Places one bar as the seed's <c>REBAR</c> block, sized and lengthed from the part
    /// number. Returns false when the seed has no such block, so the caller can fall back to
    /// a line rather than lose the bar.
    /// </summary>
    private bool TryInsertRebarBlock(RebarGroup group, Point3d a, Point3d b, string layer)
    {
        try
        {
            var table = (BlockTable)_tr.GetObject(_db.BlockTableId, OpenMode.ForRead);
            if (!table.Has(_standards.Blocks.Rebar)) return false;

            // RSX 05 480 -> a #5 bar, 480" long. Length comes from the geometry we just
            // computed rather than the name, so a spliced or trimmed bar stays honest.
            var digits = group.PartNumber.Length >= 5 ? group.PartNumber.Substring(3, 2) : "";
            if (!int.TryParse(digits, out var size) || size <= 0) return false;

            var reference = new BlockReference(a, table[_standards.Blocks.Rebar])
            {
                Layer = layer,
                Rotation = Math.Atan2(b.Y - a.Y, b.X - a.X)
            };
            Append(reference);

            if (!reference.IsDynamicBlock) return true;

            foreach (DynamicBlockReferenceProperty property in reference.DynamicBlockReferencePropertyCollection)
            {
                if (property.PropertyName.StartsWith("Visibility", StringComparison.OrdinalIgnoreCase))
                {
                    var wanted = property.GetAllowedValues()
                        .Select(v => Convert.ToString(v) ?? "")
                        .FirstOrDefault(v => v.Equals("#" + size, StringComparison.OrdinalIgnoreCase));
                    if (wanted != null) property.Value = wanted;
                }
                else if (property.PropertyName.Equals("Distance1", StringComparison.OrdinalIgnoreCase))
                {
                    property.Value = a.DistanceTo(b);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            if (_reportedRebarBlock) return false;
            _reportedRebarBlock = true;
            _log.Warn($"REBAR block unavailable ({ex.Message}); bars are drawn as lines instead.");
            return false;
        }
    }

    private bool _reportedRebarBlock;

    private ObjectId DashedLinetype()
    {
        var table = (LinetypeTable)_tr.GetObject(_db.LinetypeTableId, OpenMode.ForRead);
        foreach (var name in new[] { "HIDDEN", "HIDDEN2", "DASHED", "DASHED2" })
            if (table.Has(name)) return table[name];
        return ObjectId.Null;
    }

    /// <summary>
    /// Sheet 3: sections through the piece, plus the cut markers on the TIF view that point
    /// at them.
    /// </summary>
    private void DrawSectionsSheet(SheetWindow? sectionSheet)
    {
        if (sectionSheet == null)
        {
            _log.Warn("No section sheet found; sections were not drawn.");
            return;
        }

        var drawer = new SectionDrawer(
            _db, _tr, _ticket, _log, _standards,
            TextStyleId, DimStyleId, Append, EnsureLayer);

        var cuts = drawer.PlanCuts(sectionSheet.All.Count);
        _log.Info($"--- {sectionSheet.Layout}: {cuts.Count} section(s) planned across " +
                  $"{sectionSheet.All.Count} window(s): " +
                  string.Join(", ", cuts.Select(c => $"{c.Letter}={c.Part.PartNumber}" + (c.With.Count > 0 ? $"+{c.With.Count}" : ""))));

        var drawn = drawer.DrawSections(sectionSheet, cuts, ClearWindow);

        // Markers go on the TIF view, at the origin the piece was drawn from. Recomputing it
        // from the seed's window put them off the piece: the window is refitted to the piece
        // and the piece sits left of centre (SheetConventions.PieceOriginX).
        var markers = 0;
        if (_tifOrigin is { } tifOrigin)
            markers = drawer.DrawCutMarkers(tifOrigin, cuts);
        else
            _log.Warn("The TIF view was not drawn; section cuts were not marked.");

        _log.Info($"    drew {drawn} section(s), placed {markers} cut marker(s)");
    }

    /// <summary>
    /// Parts with at least one instance on this face. Items the exporter could not assign
    /// a face to appear on both views rather than vanishing from the ticket.
    /// </summary>
    private List<TicketPart> PartsOnFace(string face) =>
        _ticket.Parts
            .Where(p => p.Instances.Any(i =>
                string.IsNullOrWhiteSpace(i.Face) ||
                i.Face.Equals(face, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    private void DrawViewTitle(Point3d origin, double annotationScale, string face)
    {
        var title = face.Equals("BIF", StringComparison.OrdinalIgnoreCase)
            ? _standards.Labels.BifView
            : _standards.Labels.TifView;

        var note = new MText
        {
            Contents = title,
            Location = new Point3d(origin.X + _ticket.Dimensions.LengthIn / 2.0, origin.Y - 46.0, 0),
            Attachment = AttachmentPoint.TopCenter,
            TextHeight = 5.0 * annotationScale / 64.0,
            Layer = EnsureLayer(LayerText, 7),
            TextStyleId = TextStyleId()
        };
        Append(note);
    }

    /// <summary>
    /// Insulation extent and the solid zone at the top of the piece — both are model
    /// parameters (dimension_insulation_length, Solid_Zone_Top), so they are real content
    /// rather than decoration.
    /// </summary>
    /// <summary>
    /// The three labels that tell a fabricator which way round the piece is: <c>START PANEL</c>
    /// at the datum end, <c>STOP PANEL</c> at the far end, and the boxed <c>MARK END</c> in the
    /// corner of the view.
    /// <para>
    /// These are cheap to draw and worth more than their ink: on the approved ticket
    /// <c>START PANEL</c> sits at x 532.6 against a piece ending at 530.6, and on sheet 2 at
    /// 1620.0 against a piece ending at 1608 — both at the station-zero end. That is
    /// independent confirmation of the right-to-left station convention established in
    /// increment 13 from the callout positions, arrived at from a different piece of evidence.
    /// </para>
    /// </summary>
    private void DrawEndLabels(Point3d origin, ViewWindow window, double annotationScale)
    {
        var d = _ticket.Dimensions;
        var labelHeight = 6.0 * annotationScale / 64.0;

        void EndLabel(string text, double x)
        {
            Append(new MText
            {
                Contents = text,
                Location = new Point3d(x, origin.Y - 22.0, 0),
                Rotation = Math.PI / 2,
                TextHeight = labelHeight,
                Layer = EnsureLayer(LayerText, 7),
                TextStyleId = TextStyleId()
            });
        }

        // Measured, and not symmetric: START sits 2" clear of the datum end, STOP 3.2" inside
        // the far end.
        EndLabel(_standards.Labels.StartPanel, StationX(origin, 0) + 2.0);
        EndLabel(_standards.Labels.StopPanel, StationX(origin, d.LengthIn) + 3.2);

        // "MARK END", boxed, in the top-left corner of the view window.
        var markEnd = _standards.Labels.MarkEnd;
        var markHeight = 8.625 * annotationScale / 64.0;
        var markX = window.CenterX - window.Width / 2.0 + 20.0;
        var markY = window.CenterY + window.Height / 2.0 - 20.0;

        Append(new MText
        {
            Contents = markEnd,
            Location = new Point3d(markX, markY, 0),
            Attachment = AttachmentPoint.TopLeft,
            TextHeight = markHeight,
            Layer = EnsureLayer(LayerText, 7),
            TextStyleId = TextStyleId()
        });

        var pad = markHeight * 0.5;
        var boxWidth = markEnd.Length * markHeight * 0.62 + pad * 2;
        AddRect(markX - pad, markY - markHeight - pad, markX + boxWidth - pad, markY + pad, LayerText);
    }

    private void DrawInsulationExtent(Point3d origin, double annotationScale)
    {
        var d = _ticket.Dimensions;
        if (d.InsulationLengthIn <= 0 || d.InsulationLengthIn >= d.LengthIn) return;

        // Insulation stops short of the piece; the bare strip is at the START PANEL end.
        // The model gives the insulated *length* (543" of 555"), not which end is bare, and
        // the approved ticket settles it: its 45'-3" (SNX100) dimension runs to within 1'-0"
        // of the station-0 end. So the boundary is at station Length - InsulationLength, not
        // at station InsulationLength — the two coincide on the sheet only by accident when
        // the piece is drawn the wrong way round.
        var boundaryStation = d.LengthIn - d.InsulationLengthIn;
        var x = StationX(origin, boundaryStation);
        var line = new Line(new Point3d(x, origin.Y, 0), new Point3d(x, origin.Y + d.HeightIn, 0))
        {
            Layer = EnsureLayer(LayerInsulation, 7)
        };
        Append(line);

        var labels = _standards.Labels;
        var label = new MText
        {
            Contents = $"{labels.StartInsulation} \\P{TicketUnits.FeetInches(d.InsulationLengthIn)} ({labels.InsulationPartNumber})",
            Attachment = AttachmentPoint.TopRight,
            Location = new Point3d(x - 4.0, origin.Y + d.HeightIn - 6.0, 0),
            TextHeight = 3.0 * annotationScale / 64.0,
            Layer = EnsureLayer(LayerInsulation, 7),
            TextStyleId = TextStyleId()
        };
        Append(label);

        if (d.SolidZoneTopIn > 0)
        {
            var y = origin.Y + d.HeightIn - d.SolidZoneTopIn;
            var zone = new Line(new Point3d(origin.X, y, 0), new Point3d(origin.X + d.LengthIn, y, 0))
            {
                Layer = EnsureLayer(LayerInsulation, 7)
            };
            Append(zone);

            var zoneLabel = new MText
            {
                Contents = $"{TicketUnits.FeetInches(d.SolidZoneTopIn)} {labels.SolidZone}",
                Location = new Point3d(origin.X + d.LengthIn + 4.0, y, 0),
                TextHeight = 3.0 * annotationScale / 64.0,
                Layer = EnsureLayer(LayerInsulation, 7),
                TextStyleId = TextStyleId()
            };
            Append(zoneLabel);
        }
    }


    // ------------------------------------------------------------------ clearing

    /// <summary>
    /// Removes the previous piece drawing from the main view window. Everything outside it
    /// — sections, tables, the BOM block, other jobs' leftovers — is left alone.
    /// </summary>
    private int ClearWindow(ViewWindow window)
    {
        var ms = (BlockTableRecord)_tr.GetObject(
            SymbolUtilityServices.GetBlockModelSpaceId(_db), OpenMode.ForRead);

        var doomed = new List<ObjectId>();
        foreach (ObjectId id in ms)
        {
            if (_tr.GetObject(id, OpenMode.ForRead) is not Entity entity) continue;
            var anchor = CentreOf(entity) ?? AnchorOf(entity);
            if (anchor.HasValue && window.Contains(anchor.Value)) doomed.Add(id);
        }

        foreach (var id in doomed)
        {
            var entity = (Entity)_tr.GetObject(id, OpenMode.ForWrite);
            entity.Erase();
        }

        return doomed.Count;
    }

    /// <summary>
    /// The middle of the object's extents. This is what decides whether an object belongs
    /// to a view: a hatch stores only its pattern origin, and a long polyline's first vertex
    /// can sit outside the window while its body is inside, so a single anchor point left
    /// the previous job's hatches and strips on the sheet.
    /// </summary>
    private static Point3d? CentreOf(Entity entity)
    {
        try
        {
            var e = entity.GeometricExtents;
            return new Point3d((e.MinPoint.X + e.MaxPoint.X) / 2.0, (e.MinPoint.Y + e.MaxPoint.Y) / 2.0, 0);
        }
        catch
        {
            return null; // some objects have no extents (empty text, unresolved references)
        }
    }

    /// <summary>A cheap representative point, used when extents are unavailable.</summary>
    private static Point3d? AnchorOf(Entity entity) => entity switch
    {
        Line l => l.StartPoint,
        Polyline p when p.NumberOfVertices > 0 => p.GetPoint3dAt(0),
        Circle c => c.Center,
        Arc a => a.Center,
        DBText t => t.Position,
        MText m => m.Location,
        BlockReference b => b.Position,
        Dimension d => d.TextPosition,
        Leader ld when ld.NumVertices > 0 => ld.VertexAt(0),
        MLeader ml => Safe(() => ml.GetFirstVertex(0)),
        Hatch => null,   // hatch stores a pattern origin, not a location
        _ => null
    };

    private static Point3d? Safe(Func<Point3d> f)
    {
        try { return f(); } catch { return null; }
    }

    // ------------------------------------------------------------------ geometry

    private void DrawOutline(Point3d origin)
    {
        if (_ticket.Geometry.Outline.Count < 3)
        {
            _log.Warn("No outline in the ticket; the panel body was not drawn.");
            return;
        }

        AddPolyline(_ticket.Geometry.Outline, origin, LayerPanel, closed: true);
        foreach (var opening in _ticket.Geometry.Openings)
            AddPolyline(opening, origin, LayerPanel, closed: true);

        // The client marks every blockout, inner or cut from an edge, with a diagonal cross
        // corner to corner, on both the TIF and the BIF sheet.
        foreach (var o in _openings)
        {
            AddLine(StationX(origin, o.MinX), origin.Y + o.MinY, StationX(origin, o.MaxX), origin.Y + o.MaxY, LayerPanel);
            AddLine(StationX(origin, o.MinX), origin.Y + o.MaxY, StationX(origin, o.MaxX), origin.Y + o.MinY, LayerPanel);
        }

        _log.Info($"outline: {_ticket.Geometry.Outline.Count} vertices, " +
                  $"{_ticket.Geometry.Openings.Count} inner loop(s), {_openings.Count} opening(s) marked");
    }

    private void AddPolyline(IReadOnlyList<Point2> points, Point3d origin, string layer, bool closed)
    {
        var pline = new Polyline(points.Count);
        for (var i = 0; i < points.Count; i++)
            pline.AddVertexAt(i, new Point2d(StationX(origin, points[i].X), origin.Y + points[i].Y), 0, 0, 0);

        pline.Closed = closed;
        pline.Layer = EnsureLayer(layer, 7);
        Append(pline);
    }

    // ------------------------------------------------------------------ parts

    private int DrawParts(Point3d origin, IReadOnlyList<TicketPart> parts)
    {
        var placed = 0;

        foreach (var part in parts)
        {
            foreach (var instance in part.Instances)
            {
                var point = new Point3d(StationX(origin, instance.X), origin.Y + instance.Y, 0);
                if (!_resolver.TryInsert(part.PartNumber, instance.Face, point, out var blockRef))
                    continue;

                blockRef!.Layer = EnsureLayer(LayerInsert, 7);
                Append(blockRef);
                placed++;
            }
        }

        return placed;
    }

    // ------------------------------------------------------------------ dimensions

    /// <summary>
    /// The signature convention: ordinate chains measured along X from the START PANEL end,
    /// stacked in bands, each station carrying a rotated <c>℄ (n) PARTNO (FACE)</c> label.
    /// <para>
    /// Items are split by which edge they sit on and annotated off that edge, because that is
    /// what the approved ticket does: AFIP-1035's eight <c>INS087</c> recesses sit at four
    /// stations, one at the top edge and one at the bottom of each, and the drawing carries
    /// eight separate <c>℄ (1) INS087 (TIF) ½" RECESS</c> callouts — four above the piece and
    /// four below. Grouping on X alone collapsed each pair into one callout and put it above,
    /// which both halved the count and annotated the bottom recesses from the wrong side.
    /// Items away from both edges keep grouping by station, which is where the drawing's
    /// <c>℄ (2) PAS018 (TIF)</c> comes from.
    /// </para>
    /// </summary>
    private int DrawStubChains(Point3d origin, double annotationScale, IReadOnlyList<TicketPart> parts, bool annotateOpenings)
    {
        var dimStyleId = DimStyleId();
        var chains = 0;
        var height = _ticket.Dimensions.HeightIn;

        // Every callout sits in one band hard against the piece, whatever part it names, and
        // the dimension chains stack beyond it. That is the approved ticket's arrangement and
        // it is also the compact one: giving each part its own band of callouts pushed the
        // text into the next band and overlapped the stub value it belongs to.
        var calloutRun = SheetConventions.CalloutRun(annotationScale);
        var calloutAbove = origin.Y + height + CalloutBandGap;
        var calloutBelow = origin.Y - CalloutBandGap;

        var aboveBand = height + CalloutBandGap + calloutRun + ChainBandBase;
        var belowBand = -(CalloutBandGap + calloutRun + ChainBandBase);

        _overallAbove = height + ChainBandBase;

        void NextBand(bool below)
        {
            if (below)
            {
                belowBand -= ChainBandStep;
                _belowChains++;
            }
            else
            {
                aboveBand += ChainBandStep;
                _overallAbove = aboveBand;
            }
            chains++;
        }

        foreach (var part in parts.Where(p => p.Instances.Count > 0))
        {
            // Bottom-edge items are annotated below the piece, everything else above.
            foreach (var group in part.Instances
                         .GroupBy(i => SheetConventions.IsBottomEdge(i.Y))
                         .OrderBy(g => g.Key))
            {
                var below = group.Key;
                var y = origin.Y + (below ? belowBand : aboveBand);

                var instances = group.ToList();
                var xs = instances.Select(i => i.X).Distinct().OrderBy(x => x).ToList();
                DrawChainStations(
                    origin, annotationScale, dimStyleId, xs,
                    x => StationCalloutText(part, instances.Where(i => Math.Abs(i.X - x) < 0.05).ToList()),
                    CalloutStationOffset, y, below, below ? calloutBelow : calloutAbove);
                NextBand(below);
            }
        }

        if (!annotateOpenings) return chains;

        // Openings beyond the parts, one chain per side: the START and STOP stations of every
        // opening off that edge, then a row of their widths. "22'-8¼" STOP B.O.-1" and
        // "5'-0½" (B.O.-1)" on the approved ticket.
        foreach (var below in new[] { true, false })
        {
            var groups = _openingGroups.Where(g => g.Below == below).ToList();
            if (groups.Count == 0) continue;

            var stations = groups
                .SelectMany(g => new[] { (X: g.Start, Text: $"{_standards.Labels.OpeningStart} {OpeningLabel(g.Numbers)}"),
                                         (X: g.Stop, Text: $"{_standards.Labels.OpeningStop} {OpeningLabel(g.Numbers)}") })
                .GroupBy(s => Math.Round(s.X / OpeningPlan.Tolerance))
                .Select(s => (X: s.First().X, Text: string.Join(" & ", s.Select(t => t.Text))))
                .OrderBy(s => s.X)
                .ToList();

            // An opening's label sits right beside its own station line, as "STOP B.O.-1" does
            // on the approved ticket. The parts' measured offset would put a narrow opening's
            // STOP label over its START line. The anchor is the text's far side, so the offset
            // is one text height plus a gap.
            DrawChainStations(
                origin, annotationScale, dimStyleId, stations.Select(s => s.X).ToList(),
                x => stations.First(s => Math.Abs(s.X - x) < 0.05).Text,
                (3.5 + 1.0) * annotationScale / 64.0,
                origin.Y + (below ? belowBand : aboveBand), below, below ? calloutBelow : calloutAbove);
            NextBand(below);

            // Anchored on the row itself: extension lines from the piece edge would cut through
            // the callouts and the station chain between. The row carries text on both sides of
            // its line, so it is set a quarter band further out and takes a band and a half.
            var widthY = origin.Y + (below ? belowBand - WidthRowInset : aboveBand + WidthRowInset);
            foreach (var g in groups)
            {
                var width = new RotatedDimension
                {
                    XLine1Point = new Point3d(StationX(origin, g.Start), widthY, 0),
                    XLine2Point = new Point3d(StationX(origin, g.Stop), widthY, 0),
                    DimLinePoint = new Point3d(StationX(origin, (g.Start + g.Stop) / 2.0), widthY, 0),
                    Rotation = 0,
                    DimensionText = $"<>\\X({OpeningLabel(g.Numbers)})",
                    DimensionStyle = dimStyleId,
                    Layer = EnsureLayer(LayerDims, 7)
                };
                DimensionPresets.Linear.ApplyTo(width, annotationScale);
                Append(width);
            }
            NextBand(below);
            if (below)
            {
                belowBand -= ChainBandStep / 2.0;
                _belowExtra += ChainBandStep / 2.0;
            }
            else
            {
                aboveBand += ChainBandStep / 2.0;
                _overallAbove = aboveBand;
            }
        }

        return chains;
    }

    /// <summary>
    /// Dimension bands stacked off one edge, counted the way <see cref="DrawStubChains"/>
    /// allocates them: one per part with items on that edge, and for openings a station chain
    /// plus a width row of a band and a half.
    /// </summary>
    private double ChainBands(IReadOnlyList<TicketPart> drawable, bool annotateOpenings, bool below)
    {
        var parts = drawable
            .Where(p => p.Instances.Count > 0)
            .Count(p => p.Instances.Any(i => SheetConventions.IsBottomEdge(i.Y) == below));
        var openings = annotateOpenings && _openingGroups.Any(g => g.Below == below) ? 2.5 : 0.0;
        return parts + openings;
    }

    /// <summary>Piece top up to the overall length string, as <see cref="DrawStubChains"/> leaves it.</summary>
    private double AnnotationAbove(IReadOnlyList<TicketPart> drawable, bool annotateOpenings, double annotationScale)
    {
        var bands = ChainBands(drawable, annotateOpenings, below: false);
        return bands > 0
            ? CalloutBandGap + SheetConventions.CalloutRun(annotationScale) + ChainBandBase + bands * ChainBandStep
            : ChainBandBase;
    }

    /// <summary>How far the width row sits beyond the start of its band.</summary>
    private const double WidthRowInset = ChainBandStep / 4.0;

    /// <summary>
    /// One vertical chain per opening group in the dimension column beside the START PANEL
    /// end, headed "BLOCKOUT-1": the height split at the opening's bottom and top, the span
    /// that is the opening labelled "(B.O.-1)", as the approved ticket's column reads.
    /// </summary>
    private void DrawOpeningColumns(Point3d origin, double annotationScale)
    {
        if (_openingGroups.Count == 0) return;

        var dimStyleId = DimStyleId();
        var height = _ticket.Dimensions.HeightIn;
        var edgeX = StationX(origin, 0);
        var textHeight = 3.5 * annotationScale / 64.0;

        // The first column stands where the approved sheet's does, unless our SOLID ZONE label
        // (set beside the top corner, where the approved sets it above) is in the way.
        var first = SheetConventions.RightColumnFirstOffset;
        var d = _ticket.Dimensions;
        if (d.InsulationLengthIn > 0 && d.InsulationLengthIn < d.LengthIn && d.SolidZoneTopIn > 0)
        {
            var label = $"{TicketUnits.FeetInches(d.SolidZoneTopIn)} {_standards.Labels.SolidZone}";
            first = Math.Max(first, 4.0 + label.Length * (3.0 * annotationScale / 64.0) * 0.62 + 2.0 * textHeight);
        }

        var step = SheetConventions.RightColumnStep * annotationScale / 64.0;
        var lastX = first + (_openingGroups.Count - 1) * step;
        if (lastX > RightColumnReserve)
            _log.Warn($"{_openingGroups.Count} opening columns need more than the {RightColumnReserve:0}\" kept clear beside the piece; the outer ones may overlap the title block.");

        for (var k = 0; k < _openingGroups.Count; k++)
        {
            var group = _openingGroups[k];
            var x = edgeX + first + k * step;

            foreach (var span in group.VerticalSpans(height))
            {
                var dim = new RotatedDimension
                {
                    XLine1Point = new Point3d(x, origin.Y + span.From, 0),
                    XLine2Point = new Point3d(x, origin.Y + span.To, 0),
                    DimLinePoint = new Point3d(x, origin.Y + (span.From + span.To) / 2.0, 0),
                    Rotation = Math.PI / 2,
                    DimensionStyle = dimStyleId,
                    Layer = EnsureLayer(LayerDims, 7)
                };
                if (span.Opening is int n) dim.DimensionText = $"<>\\X({OpeningLabel(n.ToString())})";
                DimensionPresets.Stub.ApplyTo(dim, annotationScale);
                Append(dim);
            }

            Append(new MText
            {
                Contents = _standards.Labels.OpeningColumnPrefix + group.Numbers,
                Location = new Point3d(x, origin.Y + height + CalloutBandGap, 0),
                Rotation = Math.PI / 2,
                Attachment = AttachmentPoint.BottomLeft,
                TextHeight = textHeight,
                Layer = EnsureLayer(LayerText, 7),
                TextStyleId = TextStyleId()
            });
        }
    }

    /// <summary>Draws one chain's ordinate dimensions and the callout at each station.</summary>
    private void DrawChainStations(
        Point3d origin,
        double annotationScale,
        ObjectId dimStyleId,
        IReadOnlyList<double> xs,
        Func<double, string> calloutText,
        double calloutOffset,
        double y,
        bool below,
        double calloutBaseline)
    {
        {
            foreach (var x in xs)
            {
                var target = new Point3d(StationX(origin, x), origin.Y, 0);
                var leaderEnd = new Point3d(StationX(origin, x), y, 0);

                // The datum is the START PANEL end, which the station convention puts at the
                // right of the sheet. Measuring from the drawing's left edge instead would
                // still print numbers, just every one of them the complement of the truth.
                var datum = new Point3d(StationX(origin, 0), origin.Y, 0);

                var dim = new OrdinateDimension
                {
                    UsingXAxis = true,
                    Origin = datum,
                    DefiningPoint = target,
                    LeaderEndPoint = leaderEnd,
                    DimensionStyle = dimStyleId,
                    Layer = EnsureLayer(LayerDims, 7)
                };

                DimensionPresets.Stub.ApplyTo(dim, annotationScale);
                Append(dim);

                // The callout belongs at its own station, alongside the dimension that locates
                // it and rotated to read up the sheet — that pairing of a stub value with a
                // vertical "℄ (n) PARTNO (FACE)" is the band that gives the approved ticket its
                // texture. One label per part type, placed at the average x with a long
                // horizontal leader, was our invention and looked nothing like it.

                // If another part already labels this station, step this one out past it
                // rather than printing over it. Rounded to the inch so near-identical
                // stations count as the same column.
                var key = (below, (long)Math.Round(x));
                _calloutRows.TryGetValue(key, out var row);
                _calloutRows[key] = row + 1;

                var rowOffset = row * (SheetConventions.CalloutRun(annotationScale) + CalloutBandGap);
                var baseline = below ? calloutBaseline - rowOffset : calloutBaseline + rowOffset;

                // Rotated to read up the sheet, so the attachment names the text's own frame:
                // "Right" is its upper end. Above the piece a callout starts at the band and
                // reads away from it; below, it must END at the band — anchored by its start,
                // a callout below the piece ran back up across the bottom edge into the panel.
                // Both keep the text on the same side of the anchor.
                var callout = new MText
                {
                    Contents = calloutText(x),
                    Location = new Point3d(StationX(origin, x) + calloutOffset, baseline, 0),
                    Rotation = Math.PI / 2,
                    Attachment = below ? AttachmentPoint.BottomRight : AttachmentPoint.BottomLeft,
                    TextHeight = 3.5 * annotationScale / 64.0,
                    Layer = EnsureLayer(LayerText, 7),
                    TextStyleId = TextStyleId()
                };
                Append(callout);
                _stationCallouts++;
            }
        }
    }

    /// <summary>
    /// The client's phrasing for the items at one station: <c>℄ (2) PAS018 (TIF)</c>.
    /// </summary>
    private static string StationCalloutText(TicketPart part, IReadOnlyList<PartInstance> instances)
    {
        var faces = instances
            .Select(i => i.Face)
            .Where(f => f.Length > 0)
            .Distinct()
            .OrderBy(f => f)
            .ToList();

        var faceText = faces.Count switch
        {
            1 => $" ({faces[0]})",
            2 => $" ({faces[0]} & {faces[1]})",
            _ => ""
        };

        return $"\\U+2104 ({instances.Count}) {part.PartNumber}{faceText}";
    }

    /// <summary>
    /// An MLeader carrying the callout text. Falls back to a plain leader line plus text if
    /// the multileader cannot be built, so a callout is never silently dropped.
    /// </summary>
    private bool DrawLeader(Point3d arrow, Point3d label, string text, double textHeight)
    {
        try
        {
            var leader = new MLeader();
            leader.SetDatabaseDefaults();
            leader.ContentType = ContentType.MTextContent;
            leader.Layer = EnsureLayer(LayerText, 7);

            var mtext = new MText
            {
                Contents = text,
                TextHeight = textHeight,
                Location = label,
                TextStyleId = TextStyleId()
            };
            leader.MText = mtext;

            var line = leader.AddLeaderLine(arrow);
            leader.SetFirstVertex(line, arrow);
            leader.SetLastVertex(line, label);
            Append(leader);
            return true;
        }
        catch (Exception ex)
        {
            _log.Warn($"MLeader unavailable ({ex.Message}); drawing a plain leader instead.");

            var line = new Line(arrow, label) { Layer = EnsureLayer(LayerText, 7) };
            Append(line);

            var mtext = new MText
            {
                Contents = text,
                TextHeight = textHeight,
                Location = new Point3d(label.X + 2.0, label.Y, 0),
                Layer = EnsureLayer(LayerText, 7),
                TextStyleId = TextStyleId()
            };
            Append(mtext);
            return true;
        }
    }

    private void DrawOverallDimensions(Point3d origin, double annotationScale)
    {
        var dimStyleId = DimStyleId();
        var length = _ticket.Dimensions.LengthIn;
        var height = _ticket.Dimensions.HeightIn;

        // Over the top of everything, as on both approved sheets. Run 20" under the piece it
        // cut straight through the callouts of every part annotated off the bottom edge.
        var overall = new RotatedDimension
        {
            XLine1Point = new Point3d(origin.X, origin.Y + height, 0),
            XLine2Point = new Point3d(origin.X + length, origin.Y + height, 0),
            DimLinePoint = new Point3d(origin.X + length / 2.0, origin.Y + _overallAbove, 0),
            Rotation = 0,
            DimensionStyle = dimStyleId,
            Layer = EnsureLayer(LayerDims, 7)
        };
        DimensionPresets.Overall.ApplyTo(overall, annotationScale);
        Append(overall);

        var vertical = new RotatedDimension
        {
            XLine1Point = origin,
            XLine2Point = new Point3d(origin.X, origin.Y + height, 0),
            DimLinePoint = new Point3d(origin.X - 22.0, origin.Y + height / 2.0, 0),
            Rotation = Math.PI / 2,
            DimensionStyle = dimStyleId,
            Layer = EnsureLayer(LayerDims, 7)
        };
        DimensionPresets.Overall.ApplyTo(vertical, annotationScale);
        Append(vertical);
    }

    // ------------------------------------------------------------------ title block + BOM

    /// <summary>Fills the attributes of the client's own title blocks on every layout.</summary>
    private int FillTitleBlocks()
    {
        // Every field is written, and any we could not resolve is written as "<?>" rather
        // than blank. A blank field reads as a broken tool; "<?>" reads as a question, and
        // it is a question GMP can answer in one sentence at the demo.
        var id = _ticket.Identity;
        var tag = _standards.TitleBlock;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [tag.Mark] = Text(id.Mark),
            [tag.Pid] = Text(id.Pid),
            [tag.DesignNo] = Text(id.DesignNo),
            [tag.Code] = Text(id.Code),
            [tag.JobNumber] = Text(id.JobNumber),
            [tag.Project] = Text(id.ProjectName),
            [tag.Plant] = Text(id.Plant),
            [tag.DrawnBy] = Text(id.DrawnBy),
            [tag.CheckedBy] = Text(id.CheckedBy),
            [tag.ProjectLead] = Text(id.ProjectLead),
            [tag.EngineerChecked] = Text(id.EngineerChecked),
            [tag.ReleasedForJobUse] = Text(id.ReleasedForJobUse),
            [tag.Revision] = Text(id.Revision),
            [tag.RequiredCount] = id.RequiredCount.ToString(),
            [tag.Length] = (_ticket.Dimensions.LengthIn / 12.0).ToString("0.00"),
            [tag.Width] = (_ticket.Dimensions.HeightIn / 12.0).ToString("0.00"),
            [tag.Weight] = Value(_ticket.Quantities.WeightLb > 0, _ticket.Quantities.WeightLb.ToString("#,##0")),
            [tag.ReleaseStrength] = Value(_ticket.Quantities.ReleaseStrengthPsi > 0, _ticket.Quantities.ReleaseStrengthPsi.ToString()),
            [tag.TwentyEightDayStrength] = Value(_ticket.Quantities.TwentyEightDayStrengthPsi > 0, _ticket.Quantities.TwentyEightDayStrengthPsi.ToString())
        };

        var filled = 0;
        var titleBlocks = 0;
        foreach (var blockRef in AllBlockReferences())
        {
            // Case-insensitive, so the seed's "t_shop2" matches the "T_SHOP" prefix too.
            if (!BlockName(blockRef).StartsWith(_standards.Blocks.TitleBlockPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            titleBlocks++;
            foreach (ObjectId attId in blockRef.AttributeCollection)
            {
                if (_tr.GetObject(attId, OpenMode.ForRead) is not AttributeReference att) continue;

                // Every field we know about is written, including the ones we could not
                // resolve. Skipping them would leave the seed's values on the sheet -
                // the previous job's numbers, presented as if they were this piece's.
                if (!values.TryGetValue(att.Tag, out var value)) continue;

                var write = (AttributeReference)_tr.GetObject(attId, OpenMode.ForWrite);
                write.TextString = value;
                filled++;
            }
        }

        if (titleBlocks == 0)
            _log.Warn($"No title block named '{_standards.Blocks.TitleBlockPrefix}...' found in the seed; the title block was not filled.");

        return filled;
    }

    /// <summary>A value, or a visible marker that we did not have one.</summary>
    private static string Value(bool known, string value) => known ? value : "<?>";

    /// <summary>As <see cref="Value"/>, for strings that may simply be empty.</summary>
    private static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? "<?>" : value!;

    /// <summary>
    /// Writes our parts into the client's existing T_S_BOM rows. Rows beyond our part
    /// count are blanked rather than left showing the previous job's numbers.
    /// </summary>
    private int FillBillOfMaterial()
    {
        var blocks = _standards.Blocks;
        var allRows = AllBlockReferences()
            .Where(b => BlockName(b).Equals(blocks.BomRow, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (allRows.Count == 0)
        {
            _log.Warn($"No {blocks.BomRow} rows found in the seed; the bill of material was not filled.");
            return 0;
        }

        // The table is two columns of rows at matching heights. Sorting by height alone
        // interleaves them, which put a concrete row in the left column. The approved sheet
        // fills the right column top-down, keeps the two concrete rows at the bottom of the
        // right column, and uses the left column last — so the rows are split by column.
        var midX = (allRows.Min(b => b.Position.X) + allRows.Max(b => b.Position.X)) / 2.0;
        var right = allRows.Where(b => b.Position.X >= midX).OrderByDescending(b => b.Position.Y).ToList();
        var left = allRows.Where(b => b.Position.X < midX).OrderByDescending(b => b.Position.Y).ToList();

        // Parts first, then rule-generated rebar, then the concrete rows, mirroring the
        // approved ticket's ordering. Derived rebar carries a trailing "*" tied to the
        // derived note, so a BOM row never looks read when it was computed. Assembly
        // hardware has no row of its own; it is named in the derived note.
        var lines = _ticket.Parts
            .Where(p => !_hardware.ContainsKey(p.PartNumber))
            .OrderBy(p => p.PartNumber, StringComparer.OrdinalIgnoreCase)
            .Select(p => (No: p.PartNumber, Qty: p.Quantity.ToString("0.##")))
            .ToList();

        lines.AddRange(_rebar
            .OrderBy(g => g.PartNumber, StringComparer.OrdinalIgnoreCase)
            .Select(g => (No: g.PartNumber + "*", Qty: g.Count.ToString())));

        var q = _ticket.Quantities;
        var concrete = new List<(string No, string Qty)>();
        if (q.TopConcreteCuYd > 0)
            concrete.Add((ConcreteRowLabel(q.TopMixCode, "TOP"), q.TopConcreteCuYd.ToString("0.00")));
        if (q.BottomConcreteCuYd > 0)
            concrete.Add((ConcreteRowLabel(q.BottomMixCode, "BOT"), q.BottomConcreteCuYd.ToString("0.00")));

        // Concrete takes the last rows of the right column; everything else fills the rest
        // of the right column top-down and then the left column.
        var concreteRows = right.Skip(Math.Max(0, right.Count - concrete.Count)).ToList();
        var itemRows = right.Take(right.Count - concreteRows.Count).Concat(left).ToList();

        var assignments = new List<(BlockReference Row, string No, string Qty)>();
        for (var i = 0; i < itemRows.Count; i++)
            assignments.Add(i < lines.Count ? (itemRows[i], lines[i].No, lines[i].Qty) : (itemRows[i], "", ""));
        for (var i = 0; i < concreteRows.Count; i++)
            assignments.Add((concreteRows[i], concrete[i].No, concrete[i].Qty));

        var written = 0;
        foreach (var (row, partNo, qty) in assignments)
        {
            foreach (ObjectId attId in row.AttributeCollection)
            {
                if (_tr.GetObject(attId, OpenMode.ForRead) is not AttributeReference att) continue;
                var write = (AttributeReference)_tr.GetObject(attId, OpenMode.ForWrite);

                if (att.Tag.Equals(blocks.BomPartNumberTag, StringComparison.OrdinalIgnoreCase)) write.TextString = partNo;
                else if (att.Tag.Equals(blocks.BomQuantityTag, StringComparison.OrdinalIgnoreCase)) write.TextString = qty;
            }

            if (partNo.Length > 0) written++;
        }

        if (lines.Count > itemRows.Count)
            _log.Warn($"{lines.Count - itemRows.Count} BOM line(s) did not fit the {itemRows.Count} item rows in the seed.");

        return written;
    }

    /// <summary>
    /// The mix code when the model supplies one, otherwise a visibly incomplete label.
    /// The approved ticket prints "CAN0760010D (TOP)"; we do not invent a code we have
    /// not read, so the drafter can see exactly what is missing.
    /// </summary>
    private string ConcreteRowLabel(string mixCode, string position) =>
        string.IsNullOrWhiteSpace(mixCode) ? $"<MIX?> ({position})" : $"{mixCode} ({position})";

    /// <summary>
    /// A visible note listing everything on this sheet that was computed rather than read,
    /// on its own layer so it can be switched off. Analysis §6.3: derived content must be
    /// marked, never presented as if it came from the model.
    /// </summary>
    private void DrawDerivedNote(Point3d origin, double annotationScale)
    {
        var items = new List<string>(_ticket.Quantities.Derived);
        if (_rebar.Count > 0) items.Add("* " + _rebarRules.Describe());

        if (string.IsNullOrWhiteSpace(_ticket.Quantities.TopMixCode) ||
            string.IsNullOrWhiteSpace(_ticket.Quantities.BottomMixCode))
            items.Add("concrete mix codes not available in the model");

        if (_ticket.Quantities.ReleaseStrengthPsi == 0 || _ticket.Quantities.TwentyEightDayStrengthPsi == 0)
            items.Add("release / 28-day strengths not available in the model");

        if (_hardware.Count > 0)
        {
            var byHost = _hardware
                .GroupBy(h => h.Value, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => $"{string.Join(", ", g.Select(h => h.Key).OrderBy(k => k, StringComparer.OrdinalIgnoreCase))} on {g.Key}");
            items.Add(
                $"not drawn and left off the BOM as assembly hardware, as on the approved ticket: " +
                $"{string.Join("; ", byHost)}");
        }

        var blockless = _ticket.Parts
            .Where(p => p.Instances.Count > 0 &&
                        !_hardware.ContainsKey(p.PartNumber) &&
                        !_resolver.CanResolve(p.PartNumber))
            .OrderBy(p => p.PartNumber, StringComparer.OrdinalIgnoreCase)
            .Select(p => $"{p.PartNumber} x{p.Instances.Count}")
            .ToList();
        if (blockless.Count > 0)
            items.Add(
                $"not drawn - no block in the seed library: {string.Join(", ", blockless)}. " +
                "Still on the BOM; add the block to the seed to draw them.");

        items.AddRange(_ticket.Warnings);

        if (items.Count == 0) return;

        var text = "DERIVED - CONFIRM WITH GMP:\\P" +
                   string.Join("\\P", items.Select(i => "- " + i));

        var note = new MText
        {
            Contents = text,
            Location = new Point3d(origin.X, origin.Y - SifOffset - SifDrop - 70.0, 0),
            TextHeight = 3.0 * annotationScale / 64.0,
            Width = _ticket.Dimensions.LengthIn * 0.6,
            Layer = EnsureLayer(LayerDerived, 1),
            TextStyleId = TextStyleId()
        };

        Append(note);
        _log.Info($"derived note lists {items.Count} item(s) needing confirmation");
    }

    private IEnumerable<BlockReference> AllBlockReferences()
    {
        var bt = (BlockTable)_tr.GetObject(_db.BlockTableId, OpenMode.ForRead);
        foreach (ObjectId btrId in bt)
        {
            var btr = (BlockTableRecord)_tr.GetObject(btrId, OpenMode.ForRead);
            if (!btr.IsLayout) continue;

            foreach (ObjectId id in btr)
            {
                if (_tr.GetObject(id, OpenMode.ForRead) is BlockReference br) yield return br;
            }
        }
    }

    private string BlockName(BlockReference br)
    {
        try
        {
            var btr = (BlockTableRecord)_tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
            if (!btr.IsAnonymous && !btr.Name.StartsWith("*")) return btr.Name;
            var dyn = (BlockTableRecord)_tr.GetObject(br.DynamicBlockTableRecord, OpenMode.ForRead);
            return dyn.Name;
        }
        catch { return ""; }
    }

    // ------------------------------------------------------------------ symbol tables

    private ObjectId DimStyleId()
    {
        var table = (DimStyleTable)_tr.GetObject(_db.DimStyleTableId, OpenMode.ForRead);
        if (table.Has(DimStyleName)) return table[DimStyleName];
        _log.Warn($"Dimension style '{DimStyleName}' not in the seed; using the current style.");
        return _db.Dimstyle;
    }

    private ObjectId TextStyleId()
    {
        var table = (TextStyleTable)_tr.GetObject(_db.TextStyleTableId, OpenMode.ForRead);
        if (table.Has(TextStyleName)) return table[TextStyleName];
        _log.Warn($"Text style '{TextStyleName}' not in the seed; using the current style.");
        return _db.Textstyle;
    }

    private string EnsureLayer(string name, short colorIndex)
    {
        var table = (LayerTable)_tr.GetObject(_db.LayerTableId, OpenMode.ForWrite);
        if (table.Has(name)) return name;

        var record = new LayerTableRecord
        {
            Name = name,
            Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex)
        };

        table.Add(record);
        _tr.AddNewlyCreatedDBObject(record, true);
        return name;
    }

    private void Append(Entity entity)
    {
        var ms = (BlockTableRecord)_tr.GetObject(
            SymbolUtilityServices.GetBlockModelSpaceId(_db), OpenMode.ForWrite);
        ms.AppendEntity(entity);
        _tr.AddNewlyCreatedDBObject(entity, true);
    }
}
