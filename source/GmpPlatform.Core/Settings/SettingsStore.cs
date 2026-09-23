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

    public static PlatformSettings Load()
    {
        GmpPlatform.Core.PlatformPaths.EnsureDirectories();
        try
        {
            if (!File.Exists(FilePath))
                return new PlatformSettings();

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<PlatformSettings>(json, JsonOptions)
                   ?? new PlatformSettings();
        }
        catch
        {
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
