using System.Text;
using Autodesk.Revit.DB;
using GmpPlatform.Core.Logging;
using GmpPlatform.Core.Models;
using GmpPlatform.Core.Settings;
using View = Autodesk.Revit.DB.View;

namespace GmpPlatform.Revit.ShopTickets;

/// <summary>
/// Shop-ticket generator: per-sheet title blocks, auto scale-to-fit,
/// and region-based layout so notes never sit on top of drawings.
/// </summary>
internal sealed class WallShopTicketGenerator
{
    private readonly Document _doc;
    private readonly Element _panel;
    private readonly ShopTicketPlan _plan;
    private readonly PlatformSettings _settings;
    private ElementId _textTypeId = ElementId.InvalidElementId;

    public WallShopTicketGenerator(Document doc, Element panel, ShopTicketPlan plan, PlatformSettings? settings = null)
    {
        _doc = doc;
        _panel = panel;
        _plan = plan;
        _settings = settings ?? SettingsStore.Load();
    }

    public ShopTicketRunResult Generate()
    {
        var createdSheets = new List<string>();
        var createdSheetIds = new List<long>();
        var createdViews = new List<string>();
        var titleBlockId = TitleBlockService.ResolveOrDefault(_doc, _settings);
        _textTypeId = ResolveTextTypeId();

        if (titleBlockId != ElementId.InvalidElementId &&
            _doc.GetElement(titleBlockId) is FamilySymbol { IsActive: false } inactive)
        {
            inactive.Activate();
        }

        using (var tx = new Transaction(_doc, "GMP — Generate Wall Shop Ticket"))
        {
            tx.Start();

            // One live section per run; only placed on the TIF sheet (a view can live on one sheet).
            ViewSection? section = TryCreatePanelSection(createdViews);

            foreach (var sheetPlan in _plan.Sheets)
            {
                var drafting = CreateDraftingView(sheetPlan, createdViews);
                DrawSheetContent(drafting, sheetPlan);

                // Create blank sheet, then place title block on EVERY sheet explicitly.
                var sheet = ViewSheet.Create(_doc, ElementId.InvalidElementId);
                sheet.Name = sheetPlan.SheetName;
                sheet.SheetNumber = EnsureUniqueSheetNumber(sheetPlan.SheetNumber);
                TitleBlockService.PlaceOnSheet(_doc, sheet, titleBlockId);

                FitViewToSheet(drafting, sheet);
                if (sheetPlan.Kind == ShopTicketSheetKind.TifFabrication && section != null)
                    FitViewToSheet(section, sheet, heightFraction: 0.85);

                PlaceViewports(sheet, drafting, section, sheetPlan.Kind);
                TryStampSheetIdentity(sheet);

                createdSheets.Add($"{sheet.SheetNumber} — {sheet.Name}");
                createdSheetIds.Add(sheet.Id.Value);
            }

            tx.Commit();
        }

        // Post-pass: ensure every created shop-ticket sheet still has the chosen title block.
        using (var tx = new Transaction(_doc, "GMP — Ensure title blocks on sheets"))
        {
            tx.Start();
            var ensured = TitleBlockService.ApplyToShopTicketSheets(_doc, titleBlockId);
            tx.Commit();
            PlatformLog.Logger.Information("Title block ensure pass touched {Count} sheet(s)", ensured);
        }

        PlatformLog.Logger.Information(
            "Shop ticket created {SheetCount} sheets for mark {Mark} (title block: {TitleBlock})",
            createdSheets.Count,
            _plan.Panel.Mark,
            TitleBlockCatalog.Describe(_doc, titleBlockId));

        return new ShopTicketRunResult
        {
            RunId = _plan.RunId,
            Success = true,
            Message =
                $"Created {createdSheets.Count} shop-ticket sheets for mark '{_plan.Panel.Mark}'.\n" +
                $"Title block: {TitleBlockCatalog.Describe(_doc, titleBlockId)}\n" +
                $"View scale: {(_settings.ViewScale <= 0 ? "auto-fit" : $"1:{_settings.ViewScale}")}",
            CreatedSheetNames = createdSheets,
            CreatedViewNames = createdViews,
            CreatedSheetIds = createdSheetIds
        };
    }

    private void FitViewToSheet(View view, ViewSheet sheet, double heightFraction = 1.0)
    {
        if (!SheetLayoutHelper.TryGetContentBounds(_doc, view, out var bounds))
        {
            if (_settings.ViewScale > 0)
                view.Scale = _settings.ViewScale;
            return;
        }

        var printable = SheetLayoutHelper.GetPrintableArea(sheet, _settings);
        var paperW = Math.Max(printable.Max.U - printable.Min.U, 0.5);
        var paperH = Math.Max((printable.Max.V - printable.Min.V) * heightFraction, 0.5);
        var contentW = Math.Max(bounds.Max.X - bounds.Min.X, 0.5);
        var contentH = Math.Max(bounds.Max.Y - bounds.Min.Y, 0.5);

        view.Scale = SheetLayoutHelper.FitScale(contentW, contentH, paperW, paperH, _settings.ViewScale);
    }

    private void PlaceViewports(ViewSheet sheet, ViewDrafting drafting, ViewSection? section, ShopTicketSheetKind kind)
    {
        var printable = SheetLayoutHelper.GetPrintableArea(sheet, _settings);

        if (kind == ShopTicketSheetKind.TifFabrication && section != null)
        {
            // Drafting left, live section right — both inside printable area.
            SheetLayoutHelper.PlaceViewport(_doc, sheet, drafting.Id, printable, 0.32, 0.50);
            SheetLayoutHelper.PlaceViewport(_doc, sheet, section.Id, printable, 0.78, 0.50);
        }
        else
        {
            SheetLayoutHelper.PlaceViewport(_doc, sheet, drafting.Id, printable, 0.48, 0.50);
        }
    }

    private void TryStampSheetIdentity(ViewSheet sheet)
    {
        TrySetParam(sheet, BuiltInParameter.SHEET_NAME, sheet.Name);

        var mark = _plan.Panel.Mark;
        if (string.IsNullOrWhiteSpace(mark)) return;

        foreach (var name in new[] { "MARK", "Mark", "PANEL MARK", "SHEET MARK" })
            TrySetParam(sheet, name, mark);
    }

    private static void TrySetParam(Element element, string name, string value)
    {
        var p = element.LookupParameter(name);
        if (p is { IsReadOnly: false } && p.StorageType == StorageType.String)
            p.Set(value);
    }

    private static void TrySetParam(Element element, BuiltInParameter bip, string value)
    {
        var p = element.get_Parameter(bip);
        if (p is { IsReadOnly: false } && p.StorageType == StorageType.String)
            p.Set(value);
    }

    private ViewDrafting CreateDraftingView(ShopTicketSheetPlan sheetPlan, List<string> createdViews)
    {
        var vdt = new FilteredElementCollector(_doc)
            .OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .First(v => v.ViewFamily == ViewFamily.Drafting);

        var view = ViewDrafting.Create(_doc, vdt.Id);
        view.Name = EnsureUniqueViewName($"{_plan.Panel.Mark}_{sheetPlan.Kind}");
        // Temporary scale; FitViewToSheet adjusts after content exists.
        view.Scale = _settings.ViewScale > 0
            ? _settings.ViewScale
            : sheetPlan.Kind == ShopTicketSheetKind.SectionsAndBom ? 24 : 48;
        createdViews.Add(view.Name);
        return view;
    }

    private void DrawSheetContent(ViewDrafting view, ShopTicketSheetPlan sheetPlan)
    {
        if (sheetPlan.Kind == ShopTicketSheetKind.SectionsAndBom)
            DrawSectionsAndBomLayout(view, sheetPlan);
        else
            DrawElevationLayout(view, sheetPlan);
    }

    /// <summary>
    /// TIF / BIF layout regions (model feet):
    /// [ geometry + joint ]  |gap|  [ notes column ]
    /// Text grows downward, so notes start at the top of the geometry band.
    /// </summary>
    private void DrawElevationLayout(ViewDrafting view, ShopTicketSheetPlan sheetPlan)
    {
        var p = _plan.Panel;
        var length = Math.Max(p.LengthFt, 4.0);
        var height = Math.Max(p.HeightFt, 2.0);
        var thickness = Math.Max(p.ThicknessFt, 0.5);
        var gap = Math.Max(_settings.NoteClearanceFt, 1.25);

        DrawInsulatedPanelElevation(view, length, height, thickness, sheetPlan.Kind);

        // Joint column clear of the panel + side strip.
        var sideW = Math.Max(thickness, 0.5);
        var geometryRight = length + 0.05 + sideW;
        var jointX = geometryRight + gap;
        DrawLine(view, new XYZ(jointX, 0, 0), new XYZ(jointX, height, 0));
        DrawLine(view, new XYZ(geometryRight, height * 0.5, 0), new XYZ(jointX + 0.4, height * 0.5, 0));
        DrawLabel(view, new XYZ(jointX + 0.2, height * 0.55, 0), "JOINT / CONN");

        if (sheetPlan.Kind == ShopTicketSheetKind.BifFinish)
        {
            var revealY = height - Math.Min(0.5, height * 0.08);
            DrawLine(view, new XYZ(0.2, revealY, 0), new XYZ(length - 0.2, revealY, 0));
            // Label in the notes column, not on the face.
            DrawLabel(view, new XYZ(jointX + 0.2, revealY, 0), "REVEAL (BIF)");
        }

        var notes = BuildNotesBlock(sheetPlan);
        var notesX = jointX + gap + 0.5;
        // Text grows downward from this point — keep it aligned with the panel top.
        DrawLabel(view, new XYZ(notesX, height, 0), notes);

        // Footer below geometry with clearance.
        DrawLabel(view, new XYZ(0, -gap, 0), $"GMP · {_plan.RunId} · {sheetPlan.SheetNumber}");
    }

    /// <summary>
    /// Sheet 3: section cells in a spaced grid; header above; BOM in a right column.
    /// </summary>
    private void DrawSectionsAndBomLayout(ViewDrafting view, ShopTicketSheetPlan sheetPlan)
    {
        var p = _plan.Panel;
        var thickness = Math.Max(p.ThicknessFt, 0.5);
        var cellGap = Math.Max(_settings.SectionCellGapFt, 1.75);
        var noteGap = Math.Max(_settings.NoteClearanceFt, 1.25);

        var sections = BuildDemoSectionSpecs(thickness);
        const int columns = 3; // fewer columns → larger cells, less crowding
        var cellWidth = Math.Max(thickness * 4.0, 2.5) + 2.0;  // includes callout gutter
        var cellHeight = Math.Max(thickness * 3.5, 2.0) + 2.2; // includes title + dim gutter

        for (var i = 0; i < sections.Count; i++)
        {
            var col = i % columns;
            var row = i / columns;
            // Geometry origin inside cell with padding for dims (above) and title (below).
            var origin = new XYZ(
                col * (cellWidth + cellGap) + 0.15,
                -row * (cellHeight + cellGap) - 0.15,
                0);
            DrawSandwichSectionCell(view, origin, sections[i], cellWidth - 0.3);
        }

        var gridWidth = columns * cellWidth + (columns - 1) * cellGap;
        var rows = (sections.Count + columns - 1) / columns;
        var gridTop = cellHeight * 0.15;
        var gridBottom = -((rows - 1) * (cellHeight + cellGap) + cellHeight);

        var header = $"{sheetPlan.Title}\nMark: {p.Mark}    Type: {p.TypeName}    Design: {p.DesignNo}\n" +
                     "SCHEMATIC SECTIONS — shapes and sizes are placeholders, NOT cut from the model.";
        // Header above the grid; text grows down toward but stops before cells via gap.
        DrawLabel(view, new XYZ(0, gridTop + noteGap + 1.2, 0), header);

        var bom = BuildBomBlock(sheetPlan);
        DrawLabel(view, new XYZ(gridWidth + noteGap, gridTop, 0), bom);

        DrawLabel(view, new XYZ(0, gridBottom - noteGap, 0), $"GMP · {_plan.RunId} · {sheetPlan.SheetNumber}");
    }

    private static IReadOnlyList<SectionSpec> BuildDemoSectionSpecs(double thicknessFt)
    {
        var t = thicknessFt;
        return new[]
        {
            new SectionSpec("A", t * 2.4, t, HasRecess: false, SolidZone: true, Label: "SOLID ZONE"),
            new SectionSpec("B", t * 2.2, t, HasRecess: true, SolidZone: false, Label: "2\" RECESS"),
            new SectionSpec("C", t * 4.5, t, HasRecess: true, SolidZone: true, Label: "POCKET"),
            new SectionSpec("C1", t * 2.2, t * 1.15, HasRecess: true, SolidZone: false, Label: null),
            new SectionSpec("D", t * 2.0, t, HasRecess: false, SolidZone: false, Label: null),
            new SectionSpec("E", t * 2.0, t, HasRecess: false, SolidZone: true, Label: "SOLID ZONE"),
            new SectionSpec("F", t * 2.0, t, HasRecess: false, SolidZone: false, Label: null),
            new SectionSpec("M", t * 3.0, t * 1.3, HasRecess: false, SolidZone: false, Label: "CORBEL"),
            new SectionSpec("H", t * 2.0, t, HasRecess: false, SolidZone: true, Label: null)
        };
    }

    private void DrawSandwichSectionCell(View view, XYZ origin, SectionSpec spec, double cellSlotWidth)
    {
        var w = spec.WidthFt;
        var h = spec.HeightFt;
        var wythe = Math.Max(h * 0.22, 0.08);
        var insulation = Math.Max(h - 2 * wythe, 0.1);

        DrawRect(view, origin, w, h);

        var y1 = origin.Y + wythe;
        var y2 = y1 + insulation;
        DrawLine(view, new XYZ(origin.X, y1, 0), new XYZ(origin.X + w, y1, 0));
        DrawLine(view, new XYZ(origin.X, y2, 0), new XYZ(origin.X + w, y2, 0));
        DrawHatch(view, new XYZ(origin.X, y1, 0), w, insulation);

        if (spec.HasRecess)
        {
            var rw = Math.Min(w * 0.18, 0.35);
            var rh = Math.Min(wythe * 0.85, 0.12);
            DrawRect(view, new XYZ(origin.X + w * 0.5 - rw * 0.5, origin.Y + h - rh, 0), rw, rh);
        }

        if (spec.SolidZone)
        {
            var sx = origin.X + w * 0.08;
            var sw = w * 0.22;
            DrawLine(view, new XYZ(sx, origin.Y + 0.02, 0), new XYZ(sx, origin.Y + h - 0.02, 0));
            DrawLine(view, new XYZ(sx + sw, origin.Y + 0.02, 0), new XYZ(sx + sw, origin.Y + h - 0.02, 0));
        }

        // Callout in the right gutter of the cell slot (not over the outline).
        var calloutX = origin.X + Math.Min(w + 0.35, cellSlotWidth - 0.2);
        DrawLine(view, new XYZ(origin.X + w, origin.Y + h * 0.5, 0), new XYZ(calloutX, origin.Y + h * 0.5, 0));
        DrawLabel(view, new XYZ(calloutX + 0.05, origin.Y + h * 0.55, 0), "SNX100");

        if (!string.IsNullOrEmpty(spec.Label))
            DrawLabel(view, new XYZ(origin.X + 0.08, origin.Y + h * 0.62, 0), spec.Label!);

        // Title below outline with clearance.
        DrawLabel(view, new XYZ(origin.X, origin.Y - 0.55, 0), $"SECTION \"{spec.Name}\"");

        // Dimension band above outline. The band is drawn to show the intended layout, but
        // the cell width is schematic, so no measurement is printed — a real-looking value
        // here would be read as a model dimension.
        var dimY = origin.Y + h + 0.35;
        DrawLine(view, new XYZ(origin.X, dimY, 0), new XYZ(origin.X + w, dimY, 0));
        DrawLine(view, new XYZ(origin.X, dimY - 0.08, 0), new XYZ(origin.X, dimY + 0.08, 0));
        DrawLine(view, new XYZ(origin.X + w, dimY - 0.08, 0), new XYZ(origin.X + w, dimY + 0.08, 0));
        DrawLabel(view, new XYZ(origin.X + w * 0.15, dimY + 0.12, 0), "(schematic)");
    }

    private void DrawInsulatedPanelElevation(
        View view,
        double length,
        double height,
        double thickness,
        ShopTicketSheetKind kind)
    {
        DrawRect(view, XYZ.Zero, length, height);

        var cx = length * 0.5;
        DrawDashed(view, new XYZ(cx, 0, 0), new XYZ(cx, height, 0), 0.35);

        var sideX = length + 0.05;
        var sideW = Math.Max(thickness, 0.5);
        DrawRect(view, new XYZ(sideX, 0, 0), sideW, height);
        var wythe = Math.Max(sideW * 0.25, 0.1);
        DrawLine(view, new XYZ(sideX + wythe, 0, 0), new XYZ(sideX + wythe, height, 0));
        DrawLine(view, new XYZ(sideX + sideW - wythe, 0, 0), new XYZ(sideX + sideW - wythe, height, 0));
        DrawHatch(view, new XYZ(sideX + wythe, 0, 0), sideW - 2 * wythe, height);

        var ow = Math.Min(2.5, length * 0.18);
        var oh = Math.Min(3.0, height * 0.28);
        var ox = length * 0.32;
        var oy = height * 0.22;
        DrawRect(view, new XYZ(ox, oy, 0), ow, oh);
        var lip = Math.Min(0.12, ow * 0.08);
        DrawRect(view, new XYZ(ox + lip, oy + lip, 0), ow - 2 * lip, oh - 2 * lip);

        const double ch = 0.2;
        DrawLine(view, new XYZ(0, ch, 0), new XYZ(ch, 0, 0));
        DrawLine(view, new XYZ(length - ch, 0, 0), new XYZ(length, ch, 0));
        DrawLine(view, new XYZ(0, height - ch, 0), new XYZ(ch, height, 0));
        DrawLine(view, new XYZ(length - ch, height, 0), new XYZ(length, height - ch, 0));

        if (kind == ShopTicketSheetKind.TifFabrication)
            DrawLabel(view, new XYZ(ox, oy + oh + 0.35, 0), "OPENING");
    }

    private string BuildNotesBlock(ShopTicketSheetPlan sheetPlan)
    {
        var p = _plan.Panel;
        var sb = new StringBuilder();
        sb.AppendLine(sheetPlan.Title);
        sb.AppendLine($"Mark: {p.Mark}" + (string.IsNullOrWhiteSpace(p.Pid) ? "" : $"    PID: {p.Pid}"));
        sb.AppendLine($"Type: {p.TypeName}");
        sb.AppendLine($"Design: {p.DesignNo}");
        sb.AppendLine($"Family: {p.FamilyName}");
        sb.AppendLine(
            $"Size: L={FormatFeetInches(p.LengthFt)}  H={FormatFeetInches(p.HeightFt)}  T={FormatFeetInches(p.ThicknessFt)}");
        if (!string.IsNullOrWhiteSpace(p.DimensionSource))
            sb.AppendLine($"Dimensions from: {p.DimensionSource}");
        if (p.NeighborMarks.Count > 0)
            sb.AppendLine("Neighbors: " + string.Join(", ", p.NeighborMarks));
        sb.AppendLine();
        sb.AppendLine("Production notes:");
        foreach (var note in sheetPlan.ProductionNotes)
            sb.AppendLine("• " + note);
        return sb.ToString().TrimEnd();
    }

    private string BuildBomBlock(ShopTicketSheetPlan sheetPlan)
    {
        var p = _plan.Panel;
        var sb = new StringBuilder();
        sb.AppendLine("Production notes:");
        foreach (var note in sheetPlan.ProductionNotes)
            sb.AppendLine("• " + note);
        sb.AppendLine();
        sb.AppendLine("Bill of Material:");
        sb.AppendLine("PART NO.          QTY");
        if (p.Parts.Count == 0)
        {
            // Never print plausible-looking part numbers we did not read from the model:
            // a confidently wrong BOM is worse than a visibly empty one.
            sb.AppendLine("(no parts resolved from the model — see run report)");
        }
        else
        {
            foreach (var part in p.Parts.Take(20))
                sb.AppendLine($"{part.PartNumber,-16} {part.Quantity,6:0.###}");

            if (p.Parts.Count > 20)
                sb.AppendLine($"... and {p.Parts.Count - 20} more (see run report)");
        }

        return sb.ToString().TrimEnd();
    }

    private void DrawRect(View view, XYZ origin, double width, double height)
    {
        var bl = origin;
        var br = new XYZ(origin.X + width, origin.Y, 0);
        var tr = new XYZ(origin.X + width, origin.Y + height, 0);
        var tl = new XYZ(origin.X, origin.Y + height, 0);
        DrawLine(view, bl, br);
        DrawLine(view, br, tr);
        DrawLine(view, tr, tl);
        DrawLine(view, tl, bl);
    }

    private void DrawHatch(View view, XYZ origin, double width, double height)
    {
        if (width <= 0.01 || height <= 0.01) return;
        var step = Math.Max(Math.Min(width, height) * 0.35, 0.15);
        // Cap segment count so tall panels (40'+ columns) stay responsive.
        var maxLines = 40;
        var est = (int)Math.Ceiling((width + height) / step);
        if (est > maxLines)
            step = (width + height) / maxLines;

        var drawn = 0;
        for (var x = -height; x < width && drawn < maxLines; x += step)
        {
            var x0 = origin.X + Math.Max(x, 0);
            var y0 = origin.Y + Math.Max(-x, 0);
            var x1 = origin.X + Math.Min(x + height, width);
            var y1 = origin.Y + Math.Min(height, width - x);
            if (x1 - x0 < 0.02) continue;
            DrawLine(view, new XYZ(x0, y0, 0), new XYZ(x1, y1, 0));
            drawn++;
        }
    }

    private void DrawDashed(View view, XYZ a, XYZ b, double dashLength)
    {
        var dir = b - a;
        var len = dir.GetLength();
        if (len < 1e-6) return;
        dir = dir.Normalize();
        var pos = 0.0;
        var draw = true;
        while (pos < len)
        {
            var seg = Math.Min(dashLength, len - pos);
            var p0 = a + dir * pos;
            var p1 = a + dir * (pos + seg);
            if (draw) DrawLine(view, p0, p1);
            draw = !draw;
            pos += seg;
        }
    }

    private ViewSection? TryCreatePanelSection(List<string> createdViews)
    {
        try
        {
            var bbox = _panel.get_BoundingBox(null);
            if (bbox == null) return null;

            var mid = (bbox.Min + bbox.Max) * 0.5;
            var width = Math.Abs(bbox.Max.X - bbox.Min.X);
            var depth = Math.Abs(bbox.Max.Y - bbox.Min.Y);
            var height = Math.Abs(bbox.Max.Z - bbox.Min.Z);

            var viewDir = XYZ.BasisY.Negate();
            var up = XYZ.BasisZ;
            var right = viewDir.CrossProduct(up).Normalize();

            var transform = Transform.Identity;
            transform.Origin = mid;
            transform.BasisX = right;
            transform.BasisY = up;
            transform.BasisZ = viewDir;

            var sectionBox = new BoundingBoxXYZ
            {
                Transform = transform,
                Min = new XYZ(-Math.Max(width, 1) * 0.6, -Math.Max(height, 1) * 0.6, -Math.Max(depth, 1) * 0.6),
                Max = new XYZ(Math.Max(width, 1) * 0.6, Math.Max(height, 1) * 0.6, Math.Max(depth, 1) * 0.6)
            };

            var vft = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.Section);
            if (vft == null) return null;

            var section = ViewSection.CreateSection(_doc, vft.Id, sectionBox);
            section.Name = EnsureUniqueViewName($"{_plan.Panel.Mark}_SEC");
            section.Scale = _settings.ViewScale > 0 ? _settings.ViewScale : 48;
            createdViews.Add(section.Name);
            return section;
        }
        catch (Exception ex)
        {
            PlatformLog.Logger.Warning(ex, "Could not create panel section view");
            return null;
        }
    }

    private void DrawLine(View view, XYZ a, XYZ b)
    {
        if (a.DistanceTo(b) < 1e-6) return;
        var line = Line.CreateBound(a, b);
        _doc.Create.NewDetailCurve(view, line);
    }

    private void DrawLabel(View view, XYZ origin, string text) =>
        DraftingText.CreateAutoSized(_doc, view, origin, text, _textTypeId);

    private ElementId ResolveTextTypeId()
    {
        var textTypeId = _doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
        if (textTypeId != ElementId.InvalidElementId)
            return textTypeId;

        return new FilteredElementCollector(_doc)
            .OfClass(typeof(TextNoteType))
            .FirstElementId();
    }

    private static string FormatFeetInches(double feet)
    {
        var sign = feet < 0 ? "-" : "";
        feet = Math.Abs(feet);
        var whole = (int)Math.Floor(feet);
        var inches = (feet - whole) * 12.0;
        var inchWhole = (int)Math.Floor(inches + 1e-6);
        var frac = inches - inchWhole;
        if (frac > 0.9)
        {
            inchWhole++;
            frac = 0;
        }

        if (inchWhole >= 12)
        {
            whole++;
            inchWhole -= 12;
        }

        if (frac < 0.05)
            return inchWhole == 0 ? $"{sign}{whole}'-0\"" : $"{sign}{whole}'-{inchWhole}\"";

        return $"{sign}{whole}'-{inchWhole} {NearestEighth(frac)}\"";
    }

    private static string NearestEighth(double fracInches)
    {
        var eighths = (int)Math.Round(fracInches * 8.0);
        if (eighths <= 0) return "0";
        if (eighths >= 8) return "1";
        var g = Gcd(eighths, 8);
        return $"{eighths / g}/{8 / g}";
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0)
        {
            var t = a % b;
            a = b;
            b = t;
        }

        return Math.Max(a, 1);
    }

    private string EnsureUniqueSheetNumber(string desired)
    {
        var existing = new FilteredElementCollector(_doc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>()
            .Select(s => s.SheetNumber)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!existing.Contains(desired)) return desired;
        var i = 2;
        while (existing.Contains($"{desired}-{i}")) i++;
        return $"{desired}-{i}";
    }

    private string EnsureUniqueViewName(string desired)
    {
        var existing = new FilteredElementCollector(_doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Select(v => v.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!existing.Contains(desired)) return desired;
        var i = 2;
        while (existing.Contains($"{desired}_{i}")) i++;
        return $"{desired}_{i}";
    }

    private sealed record SectionSpec(
        string Name,
        double WidthFt,
        double HeightFt,
        bool HasRecess,
        bool SolidZone,
        string? Label);
}
