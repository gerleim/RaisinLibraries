using Raisin.EventSystem;

namespace Raisin.Core;

public static class AppPaths
{
    private static Raisin.EventSystem.EventSystem? es;
    private static string? layoutFileName;

    public static string AppDataDir { get; private set; } = null!;

    public static readonly string AppDir = AppContext.BaseDirectory;

    public static string DataDir { get; private set; } = null!;

    /// <summary>
    /// Set the app name early, before SetPortable or Options.Load.
    /// </summary>
    public static void Configure(string appName)
    {
        AppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            appName);
        DataDir = AppDataDir;
    }

    /// <summary>
    /// Wire up the EventSystem for migration logging. Call after Configure.
    /// </summary>
    public static void Initialize(Raisin.EventSystem.EventSystem eventSystem, string? layoutFileName = null)
    {
        es = eventSystem;
        AppPaths.layoutFileName = layoutFileName;
    }

    /// <summary>
    /// Override DataDir to a specific path. Used by services that need to read
    /// a user's config when running under a different account (e.g., SYSTEM).
    /// Call after Configure, before Options.Load.
    /// </summary>
    public static void OverrideDataDir(string path) => DataDir = path;

    public static void SetPortable(bool portable) =>
        DataDir = portable ? AppDir : AppDataDir;

    private static string[] GetSettingsFiles() =>
        layoutFileName is not null
            ? ["options.json", "symbol-history.json", layoutFileName]
            : ["options.json", "symbol-history.json"];

    /// <summary>
    /// Moves settings files from the current DataDir to the new location.
    /// Call BEFORE Options.Save() so the subsequent save writes to the new dir.
    /// </summary>
    public static void Migrate(bool portable)
    {
        var oldDir = DataDir;
        var newDir = portable ? AppDir : AppDataDir;

        if (oldDir == newDir) return;

        Directory.CreateDirectory(newDir);

        foreach (var file in GetSettingsFiles())
        {
            var src = Path.Combine(oldDir, file);
            var dst = Path.Combine(newDir, file);
            if (File.Exists(src))
            {
                File.Copy(src, dst, overwrite: true);
                es?.Invoke(typeof(AppPaths), new LogArgs($"Migrated {file}: {oldDir} → {newDir}") { LogSeverity = LogSeverity.Verbose, Target = LogTarget.UI });
                try { File.Delete(src); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    es?.Invoke(typeof(AppPaths), new LogArgs($"Could not delete old settings file {file}: {ex.Message}")
                        { LogSeverity = LogSeverity.Verbose, Target = LogTarget.UI });
                }
            }
        }

        DataDir = newDir;
        es?.Invoke(typeof(AppPaths), new LogArgs($"Settings storage changed to {newDir}") { Target = LogTarget.UI });
    }
}
