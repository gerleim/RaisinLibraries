using System.IO;
using System.Text.Json;
using Raisin.Core;
using Raisin.EventSystem;

namespace Raisin.WPF.Base;

public static class GridSettingsService
{
    private static Raisin.EventSystem.EventSystem? es;
    private static string _filename = "grid-settings.json";
    private static string _baseDir = "";

    public static void Initialize(Raisin.EventSystem.EventSystem eventSystem, string baseDir, string filename = "grid-settings.json")
    {
        es = eventSystem;
        _baseDir = baseDir;
        _filename = filename;
    }

    private static string FilePath => Path.Combine(_baseDir, _filename);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static Dictionary<string, GridState>? _cache;

    private static Dictionary<string, GridState> LoadAll()
    {
        if (_cache is not null)
            return _cache;

        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                _cache = JsonSerializer.Deserialize<Dictionary<string, GridState>>(json, JsonOptions);
            }
        }
        catch (Exception ex) { es?.Log(typeof(GridSettingsService), $"Load error: {ex.Message}", LogTarget.File, LogSeverity.Warning); }

        _cache ??= [];
        return _cache;
    }

    public static GridState? Load(string key)
    {
        var all = LoadAll();
        return all.GetValueOrDefault(key);
    }

    public static void Save(string key, GridState state)
    {
        var all = LoadAll();
        all[key] = state;
        WriteFile(all);
    }

    public static void Remove(string key)
    {
        var all = LoadAll();
        if (all.Remove(key))
            WriteFile(all);
    }

    public static void Prune(HashSet<string> liveContentIds)
    {
        var all = LoadAll();
        var keysToRemove = all.Keys
            .Where(k =>
            {
                var dot = k.LastIndexOf('.');
                if (dot < 0) return true;
                var contentId = k[..dot];
                return !liveContentIds.Contains(contentId);
            })
            .ToList();

        if (keysToRemove.Count == 0)
            return;

        foreach (var key in keysToRemove)
            all.Remove(key);

        WriteFile(all);
    }

    private static void WriteFile(Dictionary<string, GridState> data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, JsonOptions);
            SafeFile.WriteAllText(FilePath, json);
        }
        catch (Exception ex) { es?.Log(typeof(GridSettingsService), $"Save error: {ex.Message}", LogTarget.File, LogSeverity.Warning); }
    }
}

/// <summary>
/// How a column decides its width. A mode is a standing instruction re-applied on every load, so a
/// column set to <see cref="Content"/> re-measures against today's values instead of restoring a
/// number measured on some earlier day.
/// </summary>
public enum ColumnSizeMode
{
    /// <summary>An exact pixel width, as left by dragging the gripper.</summary>
    Fixed,
    /// <summary>Wide enough for the header text.</summary>
    Header,
    /// <summary>Wide enough for the cells, ignoring the header.</summary>
    Content,
    /// <summary>Wide enough for whichever of the two is wider — WPF's Auto.</summary>
    Both,
}

public class ColumnSetting
{
    public ColumnSizeMode Mode { get; set; } = ColumnSizeMode.Fixed;

    /// <summary>Pixels. Only meaningful for <see cref="ColumnSizeMode.Fixed"/>.</summary>
    public double Width { get; set; }
}

public class GridState
{
    /// <summary>
    /// Widths by column INDEX, written by versions before per-column settings existed. Read as a
    /// fallback when <see cref="Columns"/> is empty so an upgrade does not reset anyone's layout;
    /// never written any more.
    /// </summary>
    public Dictionary<int, double> ColumnWidths { get; set; } = [];

    /// <summary>
    /// Per-column settings keyed by column identity rather than position, so reordering columns no
    /// longer hands each one its neighbour's width.
    /// </summary>
    public Dictionary<string, ColumnSetting> Columns { get; set; } = [];
}
