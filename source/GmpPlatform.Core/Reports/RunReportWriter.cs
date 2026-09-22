using System.Text;
using System.Text.Json;
using GmpPlatform.Core.Models;

namespace GmpPlatform.Core.Reports;

/// <summary>
/// Writes a per-run record of what the generator was given and what it produced.
/// One folder per run under <see cref="PlatformPaths.Reports"/>, containing run.json
/// (machine readable) and run.html (for a quick look).
/// </summary>
public static class RunReportWriter
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>Writes the run report and returns the folder it was written to.</summary>
    public static string Write(ShopTicketPlan plan, ShopTicketRunResult result)
    {
        PlatformPaths.EnsureDirectories();

        var runId = string.IsNullOrWhiteSpace(plan.RunId)
            ? DateTime.Now.ToString("yyyyMMdd-HHmmss")
            : plan.RunId;

        var folder = Path.Combine(PlatformPaths.Reports, Sanitize(runId));
        Directory.CreateDirectory(folder);

        File.WriteAllText(
            Path.Combine(folder, "run.json"),
            JsonSerializer.Serialize(new { plan, result }, Json));

        File.WriteAllText(Path.Combine(folder, "run.html"), BuildHtml(plan, result), Encoding.UTF8);

        return folder;
    }

    private static string BuildHtml(ShopTicketPlan plan, ShopTicketRunResult result)
    {
        var p = plan.Panel;
        var sb = new StringBuilder();

        sb.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\">");
        sb.AppendLine($"<title>GMP shop ticket run {Escape(plan.RunId)}</title>");
        sb.AppendLine("<style>"
                      + "body{font:14px/1.5 system-ui,Segoe UI,sans-serif;margin:2rem;color:#111}"
                      + "h1{font-size:1.25rem;margin:0 0 .25rem}h2{font-size:1rem;margin:1.5rem 0 .5rem}"
                      + "table{border-collapse:collapse;margin:.5rem 0}td,th{border:1px solid #ccc;padding:.25rem .6rem;text-align:left}"
                      + ".ok{color:#0a7}.bad{color:#c30}.warn{background:#fff6d9}"
                      + "code{background:#f3f3f3;padding:.1rem .3rem}</style></head><body>");

        sb.AppendLine($"<h1>Shop ticket run — {Escape(p.Mark)}</h1>");
        sb.AppendLine($"<div class=\"{(result.Success ? "ok" : "bad")}\">"
                      + $"{(result.Success ? "SUCCESS" : "FAILED")} &middot; {Escape(plan.RunId)} &middot; "
                      + $"{plan.CreatedUtc:yyyy-MM-dd HH:mm} UTC</div>");

        sb.AppendLine("<h2>Panel</h2><table>");
        Row(sb, "Mark", p.Mark);
        Row(sb, "Element id", p.ElementId.ToString());
        Row(sb, "Category", p.CategoryName);
        Row(sb, "Family : Type", $"{p.FamilyName} : {p.TypeName}");
        Row(sb, "Design no.", p.DesignNo);
        Row(sb, "Length (ft)", p.LengthFt.ToString("0.###"));
        Row(sb, "Height (ft)", p.HeightFt.ToString("0.###"));
        Row(sb, "Thickness (ft)", p.ThicknessFt.ToString("0.###"));
        Row(sb, "Document", p.DocumentTitle);
        sb.AppendLine("</table>");

        sb.AppendLine("<h2>Parts read from the model</h2>");
        if (p.Parts.Count == 0)
        {
            sb.AppendLine("<p class=\"bad\">No parts resolved. The bill of material on the sheet is incomplete.</p>");
        }
        else
        {
            sb.AppendLine("<table><tr><th>Part no.</th><th>Qty</th><th>Source</th></tr>");
            foreach (var part in p.Parts)
                sb.AppendLine($"<tr><td>{Escape(part.PartNumber)}</td><td>{part.Quantity:0.###}</td>"
                              + $"<td>{Escape(part.Source)}</td></tr>");
            sb.AppendLine("</table>");
        }

        sb.AppendLine("<h2>Sheets created</h2><ul>");
        foreach (var sheet in result.CreatedSheetNames) sb.AppendLine($"<li>{Escape(sheet)}</li>");
        sb.AppendLine("</ul>");

        sb.AppendLine("<h2>Views created</h2><ul>");
        foreach (var view in result.CreatedViewNames) sb.AppendLine($"<li>{Escape(view)}</li>");
        sb.AppendLine("</ul>");

        if (plan.Warnings.Count > 0)
        {
            sb.AppendLine("<h2>Warnings</h2><table class=\"warn\">");
            foreach (var warning in plan.Warnings) sb.AppendLine($"<tr><td>{Escape(warning)}</td></tr>");
            sb.AppendLine("</table>");
        }

        sb.AppendLine($"<h2>Result</h2><pre>{Escape(result.Message)}</pre>");
        sb.AppendLine($"<p><small>{Escape(plan.DemoNote)}</small></p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static void Row(StringBuilder sb, string label, string? value) =>
        sb.AppendLine($"<tr><th>{Escape(label)}</th><td>{Escape(value ?? "")}</td></tr>");

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
