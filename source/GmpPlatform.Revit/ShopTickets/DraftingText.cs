using Autodesk.Revit.DB;
using View = Autodesk.Revit.DB.View;

namespace GmpPlatform.Revit.ShopTickets;

/// <summary>
/// Best-practice text notes for drafting views.
/// Prefer unwrapped notes (auto-size to content). Only wrap when a paper-space column width is required.
/// </summary>
internal static class DraftingText
{
    /// <summary>
    /// Creates an unwrapped text note. Width auto-fits the longest line — correct for labels and
    /// pre-broken multi-line blocks (notes/BOM with embedded newlines).
    /// </summary>
    public static TextNote? CreateAutoSized(
        Document doc,
        View view,
        XYZ origin,
        string text,
        ElementId textTypeId)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var options = new TextNoteOptions(textTypeId)
        {
            HorizontalAlignment = HorizontalTextAlignment.Left,
            KeepRotatedTextReadable = true
        };

        return TextNote.Create(doc, view.Id, origin, text, options);
    }

    /// <summary>
    /// Creates a wrapping note sized in <b>paper space feet</b> (printed width), not model feet.
    /// Use only when text must wrap inside a fixed column; otherwise prefer <see cref="CreateAutoSized"/>.
    /// </summary>
    public static TextNote? CreateWrapped(
        Document doc,
        View view,
        XYZ origin,
        string text,
        ElementId textTypeId,
        double paperWidthFt)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var options = new TextNoteOptions(textTypeId)
        {
            HorizontalAlignment = HorizontalTextAlignment.Left,
            KeepRotatedTextReadable = true
        };

        var minW = TextNote.GetMinimumAllowedWidth(doc, textTypeId);
        var maxW = TextNote.GetMaximumAllowedWidth(doc, textTypeId);

        // Fit to content when possible: estimate paper width from longest line, then cap by column budget.
        var contentPaperWidth = EstimatePaperWidthFt(doc, textTypeId, text);
        var width = Math.Min(paperWidthFt, contentPaperWidth);
        width = Math.Clamp(width, minW, maxW);

        // Zero width means unwrapped — avoid if caller asked for wrapping with a positive budget.
        if (width <= minW * 1.01 && contentPaperWidth <= minW * 1.5)
            return TextNote.Create(doc, view.Id, origin, text, options);

        var note = TextNote.Create(doc, view.Id, origin, width, text, options);
        TightenWidthToContent(note, minW, maxW);
        return note;
    }

    /// <summary>Shrinks the note box after creation so it is not larger than the drawn glyphs.</summary>
    public static void TightenWidthToContent(TextNote note, double minW, double maxW)
    {
        try
        {
            // After creation Revit knows the real line widths; pull Width down if wrapping left slack.
            var current = note.Width;
            if (current <= 0) return;

            // Prefer the API minimum that still fits — get_Parameter / Coord bbox as fallback.
            var bb = note.get_BoundingBox(note.Document.GetElement(note.OwnerViewId) as View);
            if (bb == null) return;

            // Convert model bbox width back toward paper space via view scale.
            if (note.Document.GetElement(note.OwnerViewId) is not View view) return;
            var modelW = Math.Abs(bb.Max.X - bb.Min.X);
            var paperFromBox = modelW / Math.Max(view.Scale, 1);
            // Only shrink; never expand past the box we already set.
            var tightened = Math.Clamp(Math.Min(current, paperFromBox * 1.08), minW, maxW);
            if (tightened < current * 0.98)
                note.Width = tightened;
        }
        catch
        {
            // Leave Revit's width if tightening is rejected.
        }
    }

    /// <summary>
    /// Rough paper-space width (ft) for the longest line, based on text type size.
    /// Good enough to avoid oversized wrap boxes; unwrapped create is still preferred for labels.
    /// </summary>
    public static double EstimatePaperWidthFt(Document doc, ElementId textTypeId, string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n');
        var maxChars = lines.Length == 0 ? 1 : Math.Max(1, lines.Max(l => l.Length));

        var type = doc.GetElement(textTypeId) as TextNoteType;
        var textSize = type?.get_Parameter(BuiltInParameter.TEXT_SIZE)?.AsDouble() ?? (3.0 / 32.0 / 12.0);

        // Average Latin glyph ≈ 0.55 × em height for Arial-like faces.
        var estimated = maxChars * textSize * 0.55;
        // Small pad so the last character is not clipped.
        return estimated * 1.08;
    }
}
