using Serilog;
using Serilog.Core;

namespace GmpPlatform.Core.Logging;

public static class PlatformLog
{
    private static Logger? _logger;

    public static ILogger Logger =>
        _logger ?? throw new InvalidOperationException("PlatformLog.Initialize was not called.");

    public static void Initialize(string? revitVersion = null)
    {
        PlatformPaths.EnsureDirectories();
        var logPath = Path.Combine(PlatformPaths.Logs, "gmp-platform-.log");

        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithProperty("RevitVersion", revitVersion ?? "n/a")
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .CreateLogger();
    }

    public static void Shutdown()
    {
        _logger?.Dispose();
        _logger = null;
    }
}
