using System.Text.Json;
using System.Text.Json.Serialization;

namespace GmpPlatform.Core.Settings;

/// <summary>Loads and saves <see cref="PlatformSettings"/> under ProgramData config.</summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

        // This file is meant to be edited by hand, so "SeedDwgPath" has to work as well as
        // "seedDwgPath". Without this a hand-added setting is silently ignored and the
        // feature it configures just appears not to work.
        PropertyNameCaseInsensitive = true
    };

    public static string FilePath => Path.Combine(GmpPlatform.Core.PlatformPaths.Config, "settings.json");

    /// <summary>
    /// Why the last <see cref="Load"/> fell back to defaults, or null when the file was read
    /// (or simply does not exist yet). Falling back keeps Revit usable, but a hand-edited
    /// file with a typo must not look the same as "nothing configured".
    /// </summary>
    public static string? LastLoadError { get; private set; }

    public static PlatformSettings Load()
    {
        GmpPlatform.Core.PlatformPaths.EnsureDirectories();
        LastLoadError = null;
        try
        {
            if (!File.Exists(FilePath))
                return new PlatformSettings();

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<PlatformSettings>(json, JsonOptions)
                   ?? new PlatformSettings();
        }
        catch (Exception ex)
        {
            LastLoadError = $"{ex.GetType().Name}: {ex.Message}";
            return new PlatformSettings();
        }
    }

    public static void Save(PlatformSettings settings)
    {
        GmpPlatform.Core.PlatformPaths.EnsureDirectories();
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(FilePath, json);
    }
}
