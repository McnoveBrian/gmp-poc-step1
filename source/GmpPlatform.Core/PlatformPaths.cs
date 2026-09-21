namespace GmpPlatform.Core;

/// <summary>Standard folder layout under ProgramData (override via GMP_PLATFORM_ROOT).</summary>
public static class PlatformPaths
{
    public const string RootFolderName = "GMP";
    public const string PlatformFolderName = "Platform";

    public static string Root =>
        Environment.GetEnvironmentVariable("GMP_PLATFORM_ROOT")
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            RootFolderName,
            PlatformFolderName);

    public static string Config => Path.Combine(Root, "config");
    public static string Logs => Path.Combine(Root, "logs");
    public static string Reports => Path.Combine(Root, "reports");
    public static string Exports => Path.Combine(Root, "exports");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(Config);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Reports);
        Directory.CreateDirectory(Exports);
    }
}
