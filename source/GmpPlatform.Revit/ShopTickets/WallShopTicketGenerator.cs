using System.Text;
using Autodesk.Revit.DB;
using GmpPlatform.Core.Logging;
using GmpPlatform.Core.Models;
using GmpPlatform.Core.Settings;
using View = Autodesk.Revit.DB.View;

namespace GmpPlatform.Revit.ShopTickets;

/// <summary>
/// Shop-ticket generator: title-block sheets, non-overlapping note/section layout,
/// and sandwich-panel outlines closer to AFIP-style detailing.
/// </summary>
internal sealed class WallShopTicketGenerator
{
    private readonly Document _doc;
    private readonly Element _panel;
    private readonly ShopTicketPlan _plan;
    private readonly PlatformSettings _settings;

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
        var createdViews = new List<string>();
        var titleBlockId = TitleBlockCatalog.ResolvePreferredId(_doc, _settings);

        if (titleBlockId != ElementId.InvalidElementId &&
            _doc.GetElement(titleBlockId) is FamilySymbol { IsActive: false } inactive)
        {
            inactive.Activate();
        }

        using (var tx = new Transaction(_doc, "GMP — Generate Wall Shop Ticket"))
        {
            tx.Start();

            ViewSection? section = TryCreatePanelSection(createdViews);

            foreach (var sheetPlan in _plan.Sheets)
            {
                var drafting = CreateDraftingView(sheetPlan, createdViews);
                DrawSheetContent(drafting, sheetPlan);

                var sheet = ViewSheet.Create(_doc, titleBlockId);
                sheet.Name = sheetPlan.SheetName;
                sheet.SheetNumber = EnsureUniqueSheetNumber(sheetPlan.SheetNumber);

                PlaceViewports(sheet, drafting, section, sheetPlan.Kind);
                TryStampSheetIdentity(sheet);

                createdSheets.Add($"{sheet.SheetNumber} — {sheet.Name}");
            }

            tx.Commit();
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
                $"Title block: {TitleBlockCatalog.Describe(_doc, titleBlockId)}",
            CreatedSheetNames = createdSheets,
            CreatedViewNames = createdViews
        };
    }

    private void PlaceViewports(ViewSheet sheet, ViewDrafting drafting, ViewSection? section, ShopTicketSheetKind kind)
    {
        var outline = sheet.Outline;
        var minU = outline.Min.U;
        var maxU = outline.Max.U;
        var minV = outline.Min.V;
        var maxV = outline.Max.V;
        var width = Math.Max(maxU - minU, 1.0);
        var height = Math.Max(maxV - minV, 1.0);
        var reserve = Math.Clamp(_settings.TitleBlockRightReserve, 0.15, 0.4);
        var contentMaxU = minU + width * (1.0 - reserve);

        // Primary drafting layout sits in the left/center printable area (away from title block).
        var draftingOrigin = new XYZ(
            minU + (contentMaxU - minU) * 0.38,
            minV + height * 0.48,
            0);
        Viewport.Create(_doc, sheet.Id, drafting.Id, draftingOrigin);

        // Live section cut goes on TIF only, to the right of drafting but left of title block.
        if (kind == ShopTicketSheetKind.TifFabrication && section != null)
        {
            var sectionOrigin = new XYZ(
                minU + (contentMaxU - minU) * 0.78,
                minV + height * 0.42,
                0);
            Viewport.Create(_doc, sheet.Id, section.Id, sectionOrigin);
        }
    }

    private void TryStampSheetIdentity(ViewSheet sheet)
    {
        TrySetParam(sheet, "Sheet Name", sheet.Name);
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
        view.Scale = sheetPlan.Kind == ShopTicketSheetKind.SectionsAndBom ? 24 : 48;
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

    /// <summary>TIF / BIF: sandwich elevation outline with notes in a clear left column.</summary>
    private void DrawElevationLayout(ViewDrafting view, ShopTicketSheetPlan sheetPlan)
    {
        var p = _plan.Panel;
        var length = Math.Max(p.LengthFt, 4.0);
        var height = Math.Max(p.HeightFt, 2.0);
        var thickness = Math.Max(p.ThicknessFt, 0.5);
        var gap = Math.Max(_settings.NoteClearanceFt, 0.75);

        // Geometry origin at (0,0). Notes occupy x < -gap.
        DrawInsulatedPanelElevation(view, length, height, thickness, sheetPlan.Kind);

        var jointX = length + gap;
        DrawLine(view, new XYZ(jointX, 0, 0), new XYZ(jointX, height, 0));
        DrawLine(view, new XYZ(length, height * 0.5, 0), new XYZ(jointX + 0.35, height * 0.5, 0));
        DrawText(view, new XYZ(jointX + 0.15, height * 0.5 + 0.2, 0), "JOINT / CONN");

        if (sheetPlan.Kind == ShopTicketSheetKind.BifFinish)
        {
            var revealY = height - Math.Min(0.5, height * 0.08);
            DrawLine(view, new XYZ(0.2, revealY, 0), new XYZ(length - 0.2, revealY, 0));
            DrawText(view, new XYZ(0.25, revealY - gap * 0.35, 0), "REVEAL (BIF)");
        }

        var notes = BuildNotesBlock(sheetPlan, includeBom: false);
        var noteOrigin = new XYZ(-(EstimateNoteWidth(notes) + gap), height, 0);
        DrawText(view, noteOrigin, notes);

        DrawText(
            view,
            new XYZ(0, -gap * 0.55, 0),
            $"GMP · {_plan.RunId} · {sheetPlan.SheetNumber}");
    }

    /// <summary>
    /// Sheet 3: AFIP-like sandwich section cells in a grid with gaps, BOM to the right.
    /// </summary>
    private void DrawSectionsAndBomLayout(ViewDrafting view, ShopTicketSheetPlan sheetPlan)
    {
        var p = _plan.Panel;
        var thickness = Math.Max(p.ThicknessFt, 0.5);
        var cellGap = Math.Max(_settings.SectionCellGapFt, 1.0);
        var noteGap = Math.Max(_settings.NoteClearanceFt, 0.75);

        // Typical AFIP sheet-3 section footprints (model space feet, roughly to panel thickness).
        var sections = BuildDemoSectionSpecs(thickness);
        const int columns = 4;
        var cellWidth = thickness * 3.2 + 1.2;
        var cellHeight = thickness * 2.8 + 1.8;

        for (var i = 0; i < sections.Count; i++)
        {
            var col = i % columns;
            var row = i / columns;
            var origin = new XYZ(col * (cellWidth + cellGap), -row * (cellHeight + cellGap), 0);
            DrawSandwichSectionCell(view, origin, sections[i]);
        }

        var gridWidth = columns * cellWidth + (columns - 1) * cellGap;
        var rows = (sections.Count + columns - 1) / columns;
        var gridBottom = -((rows - 1) * (cellHeight + cellGap) + cellHeight);

        var bom = BuildBomBlock(sheetPlan);
        var bomOrigin = new XYZ(gridWidth + noteGap + 0.5, 0.5, 0);
        DrawText(view, bomOrigin, bom);

        var header = $"{sheetPlan.Title}\nMark: {p.Mark}    Type: {p.TypeName}    Design: {p.DesignNo}";
        DrawText(view, new XYZ(0, cellHeight * 0.35 + noteGap, 0), header);

        DrawText(
            view,
            new XYZ(0, gridBottom - noteGap * 0.6, 0),
            $"GMP · {_plan.RunId} · {sheetPlan.SheetNumber}");
    }

    private static IReadOnlyList<SectionSpec> BuildDemoSectionSpecs(double thicknessFt)
    {
        // Outline proportions inspired by AFIP-1035 sheet 3 (A/B/C/C1…).
        var t = thicknessFt;
        return new[]
        {
            new SectionSpec("A", t * 2.4, t, HasRecess: false, SolidZone: true, Label: "SOLID ZONE"),
            new SectionSpec("B", t * 2.2, t, HasRecess: true, SolidZone: false, Label: "2\" RECESS"),
            new SectionSpec("D", t * 2.0, t, HasRecess: false, SolidZone: false, Label: null),
            new SectionSpec("E", t * 2.0, t, HasRecess: true, SolidZone: true, Label: "SOLID ZONE"),
            new SectionSpec("C", t * 5.5, t, HasRecess: true, SolidZone: true, Label: "POCKET"),
            new SectionSpec("C1", t * 2.2, t * 1.15, HasRecess: true, SolidZone: false, Label: null),
            new SectionSpec("C2", t * 2.2, t * 1.15, HasRecess: true, SolidZone: false, Label: null),
            new SectionSpec("F", t * 2.0, t, HasRecess: false, SolidZone: false, Label: null),
            new SectionSpec("M", t * 3.2, t * 1.4, HasRecess: false, SolidZone: false, Label: "CORBEL"),
            new SectionSpec("H", t * 2.0, t, HasRecess: false, SolidZone: true, Label: null),
            new SectionSpec("N", t * 2.4, t, HasRecess: false, SolidZone: false, Label: null),
            new SectionSpec("S", t * 2.0, t, HasRecess: false, SolidZone: false, Label: "SEC S")
        };
    }

    private void DrawSandwichSectionCell(View view, XYZ origin, SectionSpec spec)
    {
        var w = spec.WidthFt;
        var h = spec.HeightFt;
        var wythe = Math.Max(h * 0.22, 0.08);
        var insulation = Math.Max(h - 2 * wythe, 0.1);

        // Outer outline
        DrawRect(view, origin, w, h);

        // Inner wythe / insulation / outer wythe bands (horizontal sandwich)
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

        // Part callout to the right of the cell (clear of outline)
        var callout = new XYZ(origin.X + w + 0.25, origin.Y + h * 0.55, 0);
        DrawLine(view, new XYZ(origin.X + w, origin.Y + h * 0.5, 0), callout);
        DrawText(view, new XYZ(callout.X + 0.05, callout.Y, 0), "SNX100");

        if (!string.IsNullOrEmpty(spec.Label))
            DrawText(view, new XYZ(origin.X + 0.08, origin.Y + h * 0.55, 0), spec.Label!);

        // Title centered under the cell with clearance
        var title = $"SECTION \"{spec.Name}\"";
        DrawText(view, new XYZ(origin.X + w * 0.15, origin.Y - 0.45, 0), title);

        // Simple overall dimension ticks above the cell
        var dimY = origin.Y + h + 0.28;
        DrawLine(view, new XYZ(origin.X, dimY, 0), new XYZ(origin.X + w, dimY, 0));
        DrawLine(view, new XYZ(origin.X, dimY - 0.08, 0), new XYZ(origin.X, dimY + 0.08, 0));
        DrawLine(view, new XYZ(origin.X + w, dimY - 0.08, 0), new XYZ(origin.X + w, dimY + 0.08, 0));
        DrawText(view, new XYZ(origin.X + w * 0.25, dimY + 0.05, 0), FormatFeetInches(w));
    }

    private void DrawInsulatedPanelElevation(
        View view,
        double length,
        double height,
        double thickness,
        ShopTicketSheetKind kind)
    {
        // Main face outline
        DrawRect(view, XYZ.Zero, length, height);

        // Centerline (dashed approximation with short segments)
        var cx = length * 0.5;
        DrawDashed(view, new XYZ(cx, 0, 0), new XYZ(cx, height, 0), 0.35);

        // Side thickness strip showing sandwich build-up (matches PDF edge look)
        var sideX = length + 0.05;
        var sideW = Math.Max(thickness, 0.5);
        DrawRect(view, new XYZ(sideX, 0, 0), sideW, height);
        var wythe = Math.Max(sideW * 0.25, 0.1);
        DrawLine(view, new XYZ(sideX + wythe, 0, 0), new XYZ(sideX + wythe, height, 0));
        DrawLine(view, new XYZ(sideX + sideW - wythe, 0, 0), new XYZ(sideX + sideW - wythe, height, 0));
        DrawHatch(view, new XYZ(sideX + wythe, 0, 0), sideW - 2 * wythe, height);

        // Opening — rectangular through-opening with recess lip
        var ow = Math.Min(2.5, length * 0.18);
        var oh = Math.Min(3.0, height * 0.28);
        var ox = length * 0.32;
        var oy = height * 0.22;
        DrawRect(view, new XYZ(ox, oy, 0), ow, oh);
        var lip = Math.Min(0.12, ow * 0.08);
        DrawRect(view, new XYZ(ox + lip, oy + lip, 0), ow - 2 * lip, oh - 2 * lip);

        // Corner chamfer ticks (fabrication cue)
        const double ch = 0.2;
        DrawLine(view, new XYZ(0, ch, 0), new XYZ(ch, 0, 0));
        DrawLine(view, new XYZ(length - ch, 0, 0), new XYZ(length, ch, 0));
        DrawLine(view, new XYZ(0, height - ch, 0), new XYZ(ch, height, 0));
        DrawLine(view, new XYZ(length - ch, height, 0), new XYZ(length, height - ch, 0));

        if (kind == ShopTicketSheetKind.TifFabrication)
            DrawText(view, new XYZ(ox, oy + oh + 0.25, 0), "OPENING");
    }

    private string BuildNotesBlock(ShopTicketSheetPlan sheetPlan, bool includeBom)
    {
        var p = _plan.Panel;
        var sb = new StringBuilder();
        sb.AppendLine(sheetPlan.Title);
        sb.AppendLine($"Mark: {p.Mark}");
        sb.AppendLine($"Type: {p.TypeName}");
        sb.AppendLine($"Design: {p.DesignNo}");
        sb.AppendLine($"Family: {p.FamilyName}");
        sb.AppendLine($"Size: L={FormatFeetInches(p.LengthFt)}  H={FormatFeetInches(p.HeightFt)}  T={FormatFeetInches(p.ThicknessFt)}");
        if (p.NeighborMarks.Count > 0)
            sb.AppendLine("Neighbors: " + string.Join(", ", p.NeighborMarks));
        sb.AppendLine();
        sb.AppendLine("Production notes:");
        foreach (var note in sheetPlan.ProductionNotes)
            sb.AppendLine("• " + note);

        if (includeBom)
        {
            sb.AppendLine();
            sb.Append(BuildBomBlock(sheetPlan));
        }

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
            sb.AppendLine("RSX03143            38");
            sb.AppendLine("PAS032               2");
            sb.AppendLine("INS087               8");
            sb.AppendLine("SNX100             518");
        }
        else
        {
            foreach (var part in p.Parts.Take(20))
                sb.AppendLine($"{part.PartNumber,-16} {part.Quantity,6:0.###}");
        }

        return sb.ToString().TrimEnd();
    }

    private static double EstimateNoteWidth(string notes)
    {
        var lines = notes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var maxChars = lines.Length == 0 ? 20 : lines.Max(l => l.Length);
        // Approx drafting text width at typical note size (~0.1 ft per char at scale).
        return Math.Max(3.5, maxChars * 0.09);
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
        var step = Math.Max(Math.Min(width, height) * 0.35, 0.12);
        for (var x = -height; x < width; x += step)
        {
            var x0 = origin.X + Math.Max(x, 0);
            var y0 = origin.Y + Math.Max(-x, 0);
            var x1 = origin.X + Math.Min(x + height, width);
            var y1 = origin.Y + Math.Min(height, width - x);
            if (x1 - x0 < 0.02) continue;
            DrawLine(view, new XYZ(x0, y0, 0), new XYZ(x1, y1, 0));
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

    private void DrawText(View view, XYZ origin, string text)
    {
        var textTypeId = _doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
        if (textTypeId == ElementId.InvalidElementId)
        {
            textTypeId = new FilteredElementCollector(_doc)
                .OfClass(typeof(TextNoteType))
                .FirstElementId();
        }

        var options = new TextNoteOptions(textTypeId)
        {
            HorizontalAlignment = HorizontalTextAlignment.Left,
            KeepRotatedTextReadable = true
        };

        TextNote.Create(_doc, view.Id, origin, text, options);
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
