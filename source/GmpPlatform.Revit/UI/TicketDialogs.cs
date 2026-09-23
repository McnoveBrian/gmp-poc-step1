using System.Diagnostics;
using Autodesk.Revit.UI;
using GmpPlatform.Core.Logging;
using GmpPlatform.Core.Tickets;

namespace GmpPlatform.Revit.UI;

/// <summary>
/// Every dialog the shop-ticket commands show, in one consistent style.
/// <para>
/// The old dialogs were built ad hoc in each command and read like debug output: raw decimal
/// feet (<c>L=47.833 H=12.354 T=0.75</c>), a paragraph of warnings in the main body, and a
/// title Revit prefixed with the add-in name so it read "GMP Production Platform - GMP
/// Platform — Generate Wall Sho…". A confirmation dialog is the first thing anyone sees of this
/// tool, and in a demo it is the first thing the client sees.
/// </para>
/// <para>
/// The conventions here: sizes in feet-inches with fractions, as the ticket prints them; the
/// main body says only what is needed to decide; anything that needs reviewing is counted in
/// the body and listed under "Show details"; and choices are command links that say what they
/// do rather than a bare Yes/No.
/// </para>
/// </summary>
internal static class TicketDialogs
{
    private const string Title = "GMP Platform";

    /// <summary>What the confirmation dialog needs to know about the piece.</summary>
    internal sealed class PieceSummary
    {
        public string Mark { get; init; } = "";
        public string Family { get; init; } = "";
        public string Type { get; init; } = "";
        public double LengthIn { get; init; }
        public double HeightIn { get; init; }
        public double ThicknessIn { get; init; }
        public int PartTypes { get; init; } = -1;
        public string TitleBlock { get; init; } = "";
        public string TitleBlockNote { get; init; } = "";
        public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
    }

    /// <summary>"47'-10" long × 12'-4 1/4" high × 9" thick"</summary>
    public static string Size(double lengthIn, double heightIn, double thicknessIn) =>
        $"{TicketUnits.Architectural(lengthIn)} long × " +
        $"{TicketUnits.Architectural(heightIn)} high × " +
        $"{TicketUnits.Architectural(thicknessIn)} thick";

    /// <summary>
    /// Asks before generating. Returns true to proceed.
    /// </summary>
    /// <param name="what">What will be produced, e.g. "three Revit sheets".</param>
    /// <param name="goLabel">The proceed command link's text.</param>
    /// <param name="goDetail">One line under it saying what happens.</param>
    public static bool Confirm(PieceSummary piece, string what, string goLabel, string goDetail)
    {
        var body = new List<string>
        {
            $"{piece.Family} · {piece.Type}",
            Size(piece.LengthIn, piece.HeightIn, piece.ThicknessIn)
        };

        if (piece.PartTypes >= 0)
            body.Add($"{piece.PartTypes} part type(s) read from the model");

        body.Add("");
        body.Add(string.IsNullOrWhiteSpace(piece.TitleBlock)
            ? "Title block: none selected"
            : $"Title block: {piece.TitleBlock}");

        if (piece.Notes.Count > 0)
        {
            body.Add("");
            body.Add(piece.Notes.Count == 1
                ? "1 item will be flagged for review on the drawing — see details."
                : $"{piece.Notes.Count} items will be flagged for review on the drawing — see details.");
        }

        var dialog = new TaskDialog(Title)
        {
            TitleAutoPrefix = false,
            MainIcon = TaskDialogIcon.TaskDialogIconInformation,
            MainInstruction = $"Generate {what} for {piece.Mark}?",
            MainContent = string.Join("\n", body),
            AllowCancellation = true
        };

        if (piece.Notes.Count > 0)
        {
            dialog.ExpandedContent =
                "Flagged for review:\n\n" + string.Join("\n\n", piece.Notes.Select(n => "• " + n));
        }

        if (!string.IsNullOrWhiteSpace(piece.TitleBlockNote) && piece.TitleBlockNote != "From Settings.")
            dialog.FooterText = piece.TitleBlockNote;

        dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, goLabel, goDetail);
        dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Cancel");

        return dialog.Show() == TaskDialogResult.CommandLink1;
    }

    /// <summary>
    /// Reports a finished run. Offers to open the output, and returns nothing: the user has
    /// already seen the result by the time they close it.
    /// </summary>
    /// <param name="headline">e.g. "Shop ticket created for FIP-1244".</param>
    /// <param name="items">What was produced, one per line.</param>
    /// <param name="folder">Where the output is, offered as a link rather than printed as a path.</param>
    /// <param name="firstFile">Optional file to offer to open directly.</param>
    /// <param name="notes">Anything worth reviewing, shown under "Show details".</param>
    public static void Done(
        string headline,
        IReadOnlyList<string> items,
        string? folder,
        string? firstFile = null,
        string? firstFileLabel = null,
        IReadOnlyList<string>? notes = null)
    {
        var body = items.Count == 0
            ? "Nothing was produced."
            : string.Join("\n", items.Select(i => "• " + i));

        var dialog = new TaskDialog(Title)
        {
            TitleAutoPrefix = false,
            MainIcon = TaskDialogIcon.TaskDialogIconInformation,
            MainInstruction = headline,
            MainContent = body,
            CommonButtons = TaskDialogCommonButtons.Close,
            DefaultButton = TaskDialogResult.Close
        };

        if (notes is { Count: > 0 })
            dialog.ExpandedContent = "Flagged for review:\n\n" + string.Join("\n\n", notes.Select(n => "• " + n));

        if (!string.IsNullOrWhiteSpace(firstFile))
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, firstFileLabel ?? "Open");
        if (!string.IsNullOrWhiteSpace(folder))
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Open the output folder", folder);

        var choice = dialog.Show();
        if (choice == TaskDialogResult.CommandLink1 && firstFile != null) Open(firstFile);
        else if (choice == TaskDialogResult.CommandLink2 && folder != null) Open(folder);
    }

    /// <summary>A run that could not start or did not finish, said plainly.</summary>
    public static void Problem(string headline, string explanation, string? detail = null)
    {
        var dialog = new TaskDialog(Title)
        {
            TitleAutoPrefix = false,
            MainIcon = TaskDialogIcon.TaskDialogIconWarning,
            MainInstruction = headline,
            MainContent = explanation,
            CommonButtons = TaskDialogCommonButtons.Close
        };

        if (!string.IsNullOrWhiteSpace(detail))
            dialog.ExpandedContent = detail;

        dialog.Show();
    }

    private static void Open(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { PlatformLog.Logger.Warning(ex, "Could not open {Path}", path); }
    }
}
