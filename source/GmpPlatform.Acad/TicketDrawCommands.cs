using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using GmpPlatform.Core.Tickets;

namespace GmpPlatform.Acad;

/// <summary>Simple file log; accoreconsole buffers stdout until exit, so progress goes to disk.</summary>
internal sealed class DrawLog
{
    private readonly string _path;

    public DrawLog(string path, bool append = false)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var header = $"GMP ticket — {DateTime.Now:u}{Environment.NewLine}";
        if (append) File.AppendAllText(path, header);
        else File.WriteAllText(path, header);
    }

    public void Info(string message) => Write("     " + message);
    public void Warn(string message) => Write("WARN " + message);
    public void Error(string message) => Write("FAIL " + message);

    private void Write(string line)
    {
        try { File.AppendAllText(_path, line + Environment.NewLine); } catch { /* logging must never throw */ }
        Console.WriteLine(line);
    }
}

public class TicketDrawCommands
{
    /// <summary>
    /// Draws a piece ticket into the currently open drawing, which is expected to be a
    /// copy of the client's approved DWG (the seed).
    /// <para>
    /// Inputs come from environment variables so the command can run unattended under
    /// accoreconsole: GMP_TICKET_JSON (required) and GMP_DRAW_LOG (optional).
    /// </para>
    /// </summary>
    [CommandMethod("GMPTICKET")]
    public void DrawTicket()
    {
        var logPath = Environment.GetEnvironmentVariable("GMP_DRAW_LOG")
                      ?? Path.Combine(Path.GetTempPath(), "gmp-ticket-draw.log");
        var log = new DrawLog(logPath);

        try
        {
            var ticketPath = Environment.GetEnvironmentVariable("GMP_TICKET_JSON");
            if (string.IsNullOrWhiteSpace(ticketPath) || !File.Exists(ticketPath))
            {
                log.Error($"Set GMP_TICKET_JSON to a piece ticket file (got '{ticketPath}').");
                return;
            }

            var ticket = TicketJson.Read(ticketPath);
            log.Info($"ticket {ticket.Identity.Mark} from {Path.GetFileName(ticketPath)}");
            log.Info($"piece {ticket.Dimensions.LengthIn:N2} x {ticket.Dimensions.HeightIn:N2} x " +
                     $"{ticket.Dimensions.ThicknessIn:N2} in, {ticket.Parts.Count} part type(s)");

            foreach (var warning in ticket.Warnings) log.Warn("from Revit: " + warning);

            var db = HostApplicationServices.WorkingDatabase;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                new TicketDrawer(db, tr, ticket, log).Draw();
                tr.Commit();
            }

            var savePath = Environment.GetEnvironmentVariable("GMP_TICKET_DWG_OUT");
            if (!string.IsNullOrWhiteSpace(savePath))
            {
                // SaveAs onto the file AutoCAD already has open fails with eFilerError, so
                // saving in place is left to QSAVE in the script; SaveAs is only for writing
                // the result somewhere other than the drawing we opened.
                if (string.Equals(Path.GetFullPath(savePath), Path.GetFullPath(db.Filename),
                        StringComparison.OrdinalIgnoreCase))
                {
                    log.Info("drawing is the open file; saved in place by the script's QSAVE");
                }
                else
                {
                    db.SaveAs(savePath, DwgVersion.Current);
                    log.Info($"saved {savePath}");
                }
            }

            log.Info("done");
        }
        catch (System.Exception ex)
        {
            log.Error(ex.ToString());
        }
    }

    /// <summary>Reports what the engine can see in the seed, without changing anything.</summary>
    [CommandMethod("GMPSEEDINFO")]
    public void SeedInfo()
    {
        var log = new DrawLog(Environment.GetEnvironmentVariable("GMP_DRAW_LOG")
                              ?? Path.Combine(Path.GetTempPath(), "gmp-seed-info.log"));

        var db = HostApplicationServices.WorkingDatabase;
        using var tr = db.TransactionManager.StartTransaction();

        foreach (var sheet in SheetWindows.Discover(db, tr))
        {
            log.Info($"layout '{sheet.Layout}': {sheet.Viewports} viewport(s)");
            if (sheet.Main is { } main)
                log.Info($"    main window centre ({main.CenterX:N1}, {main.CenterY:N1}) " +
                         $"size {main.Width:N0} x {main.Height:N0}");
        }

        var dimStyles = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
        log.Info("dimension styles: " + string.Join(", ",
            dimStyles.Cast<ObjectId>().Select(id => ((DimStyleTableRecord)tr.GetObject(id, OpenMode.ForRead)).Name)));

        // Catalogued, not applied: the engine draws three kinds of dimension so far and uses
        // three of these presets. Reporting the catalogue's coverage as though it were the
        // drawing's would be the kind of flattering number this project keeps trying to avoid.
        log.Info($"dimension presets catalogued: {DimensionPresets.All.Count}, describing " +
                 $"{DimensionPresets.CoverageFraction:P0} of the approved drawing's overridden " +
                 $"dimensions; {DimensionPresets.InUse.Count} of them are wired to a call site");

        tr.Commit();
    }
}
