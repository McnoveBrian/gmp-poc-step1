using System.Diagnostics;
using System.Text;

namespace GmpPlatform.Core.Interop;

/// <summary>What a headless AutoCAD run produced.</summary>
public sealed class AcadRunResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string ScriptPath { get; set; } = "";
    public string LogPath { get; set; } = "";
    public string OutputDwg { get; set; } = "";
    public List<string> PdfFiles { get; set; } = new();
    public string ConsoleOutput { get; set; } = "";

    /// <summary>Non-fatal problems worth telling the user about.</summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Drives AutoCAD's headless console to draw and plot a ticket, so the whole chain runs from
/// one command in Revit instead of by hand.
/// <para>
/// Notes that cost time to learn and are easy to lose: the console must be started so it owns
/// a console of its own (it silently never runs its script otherwise), its stdout is buffered
/// until exit so progress has to go to a file, and NETLOAD stalls on an invisible trust prompt
/// unless the plugin folder is trusted or SECURELOAD is turned off in the script.
/// </para>
/// </summary>
public static class AcadRunner
{
    /// <summary>The AutoCAD release the drawing engine is compiled against.</summary>
    public const string TargetRelease = "2025";

    /// <summary>
    /// Finds accoreconsole.exe, preferring the release the drawing engine was built for.
    /// <para>
    /// Version matters: the engine links AutoCAD 2025's managed API, and picking simply the
    /// newest install sent it to AutoCAD 2027, where the run stopped part-way through without
    /// saving. A newer release is used only when the matching one is absent, and the caller
    /// is told, because that combination is not supported.
    /// </para>
    /// </summary>
    public static string? FindConsole(string? configured = null, string? preferRelease = TargetRelease)
    {
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;

        var candidates = new List<string>();
        foreach (var root in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                 })
        {
            var autodesk = Path.Combine(root, "Autodesk");
            if (!Directory.Exists(autodesk)) continue;

            candidates.AddRange(Directory.GetDirectories(autodesk, "AutoCAD *")
                .Select(d => Path.Combine(d, "accoreconsole.exe"))
                .Where(File.Exists));
        }

        if (candidates.Count == 0) return null;

        var matching = candidates.FirstOrDefault(p =>
            !string.IsNullOrWhiteSpace(preferRelease) &&
            p.IndexOf(preferRelease!, StringComparison.OrdinalIgnoreCase) >= 0);

        return matching ?? candidates.OrderByDescending(p => p).First();
    }

    /// <summary>
    /// Copies the seed, draws the ticket into the copy and plots it.
    /// </summary>
    /// <param name="ticketJson">The piece ticket produced by the Revit side.</param>
    /// <param name="seedDwg">The client drawing used as the template.</param>
    /// <param name="pluginDll">GmpPlatform.Acad.dll.</param>
    /// <param name="outputFolder">Where the DWG, PDFs and log are written.</param>
    /// <param name="consoleExe">accoreconsole.exe, or null to find it.</param>
    /// <param name="fontMap">Optional .fmp mapping for the client's missing SHX fonts.</param>
    /// <param name="plot">Whether to plot the sheets to PDF after drawing.</param>
    public static AcadRunResult Run(
        string ticketJson,
        string seedDwg,
        string pluginDll,
        string outputFolder,
        string? consoleExe = null,
        string? fontMap = null,
        bool plot = true,
        int timeoutSeconds = 300)
    {
        var result = new AcadRunResult();

        var console = FindConsole(consoleExe);
        if (console == null)
            return Fail(result, "AutoCAD's headless console (accoreconsole.exe) was not found. " +
                                "Set AutoCadConsolePath in settings.");

        if (console.IndexOf(TargetRelease, StringComparison.OrdinalIgnoreCase) < 0)
            result.Warnings.Add(
                $"Running on {Path.GetFileName(Path.GetDirectoryName(console))}, but the drawing engine " +
                $"is built for AutoCAD {TargetRelease}. Install {TargetRelease} or set AutoCadConsolePath.");

        foreach (var (path, what) in new[] { (ticketJson, "piece ticket"), (seedDwg, "seed drawing"), (pluginDll, "drawing engine") })
            if (!File.Exists(path))
                return Fail(result, $"The {what} was not found at: {path}");

        Directory.CreateDirectory(outputFolder);

        var mark = Path.GetFileNameWithoutExtension(ticketJson).Replace(".ticket", "");
        var workDwg = Path.Combine(outputFolder, mark + ".dwg");
        File.Copy(seedDwg, workDwg, overwrite: true);

        result.OutputDwg = workDwg;
        result.LogPath = Path.Combine(outputFolder, mark + ".draw.log");
        result.ScriptPath = Path.Combine(outputFolder, mark + ".scr");

        var script = new StringBuilder();
        script.AppendLine("(setvar \"SECURELOAD\" 0)");
        if (!string.IsNullOrWhiteSpace(fontMap) && File.Exists(fontMap))
            // fontMap is non-null here; netstandard2.0 lacks the NotNullWhen annotation that
            // would let the compiler see that through IsNullOrWhiteSpace.
            script.AppendLine($"(setvar \"FONTMAP\" \"{Forward(fontMap!)}\")");
        script.AppendLine($"(command \"NETLOAD\" \"{Forward(pluginDll)}\")");
        script.AppendLine("GMPTICKET");
        script.AppendLine("_QSAVE");   // the drawing being written is the one that is open
        if (plot) script.AppendLine("GMPTICKETPLOT");
        script.AppendLine("QUIT");
        File.WriteAllText(result.ScriptPath, script.ToString());

        var info = new ProcessStartInfo
        {
            FileName = console,
            // netstandard2.0 has no ArgumentList, so the command line is quoted by hand.
            Arguments = $"/i \"{workDwg}\" /s \"{result.ScriptPath}\" /l en-US",

            // The console must own a real console window. With CreateNoWindow, or with
            // stdout redirected, accoreconsole starts, opens the drawing and then silently
            // never runs its script - no error, no output, nothing drawn. UseShellExecute
            // stays false because the run is configured through environment variables.
            UseShellExecute = false,
            CreateNoWindow = false,
            WindowStyle = ProcessWindowStyle.Minimized
        };

        info.Environment["GMP_TICKET_JSON"] = ticketJson;
        info.Environment["GMP_DRAW_LOG"] = result.LogPath;
        info.Environment["GMP_TICKET_DWG_OUT"] = workDwg;
        info.Environment["GMP_PLOT_DIR"] = outputFolder;

        try
        {
            using var process = Process.Start(info);
            if (process == null) return Fail(result, "AutoCAD's console could not be started.");

            // The console does not always exit on QUIT; the work is done and saved by then,
            // so a timeout here is not a failure on its own.
            if (!process.WaitForExit(timeoutSeconds * 1000))
            {
                try { process.Kill(); } catch { /* best effort */ }
            }


        }
        catch (Exception ex)
        {
            return Fail(result, "AutoCAD's console failed to run: " + ex.Message);
        }

        result.PdfFiles = Directory.Exists(outputFolder)
            ? Directory.GetFiles(outputFolder, "*.pdf").OrderBy(f => f).ToList()
            : new List<string>();

        // The drawing engine's own log is the real evidence the work happened.
        var log = File.Exists(result.LogPath) ? File.ReadAllText(result.LogPath) : "";
        result.Success = log.IndexOf("done", StringComparison.OrdinalIgnoreCase) >= 0 &&
                         log.IndexOf("FAIL", StringComparison.Ordinal) < 0;

        result.Message = result.Success
            ? $"Drew {Path.GetFileName(workDwg)}" +
              (result.PdfFiles.Count > 0 ? $" and plotted {result.PdfFiles.Count} sheet(s)." : ".")
            : log.Length > 0
                ? "The drawing engine reported a problem. See " + result.LogPath
                : "The drawing engine produced no log; check that the plugin loaded.";

        return result;
    }

    private static AcadRunResult Fail(AcadRunResult result, string message)
    {
        result.Success = false;
        result.Message = message;
        return result;
    }

    /// <summary>AutoCAD scripts take forward slashes; backslashes need escaping in LISP strings.</summary>
    private static string Forward(string path) => path.Replace('\\', '/');
}
