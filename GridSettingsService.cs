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
        catch (Exception ex) { es?.Log(typeof(GridSettingsService), $"Load error: {ex.Message}", MessageSeverity.Warning); }

        _cache ??= [];
        return _cache;
    }

    public static GridState? Load(string key)
    {
        var all = LoadAll();
        return all.GetValueOrDefault(key);
    }

    public static void Save(string key, Dictionary<int, double> widths)
    {
        var all = LoadAll();
        all[key] = new GridState { ColumnWidths = widths };
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
        catch (Exception ex) { es?.Log(typeof(GridSettingsService), $"Save error: {ex.Message}", MessageSeverity.Warning); }
    }
}

public class GridState
{
    public Dictionary<int, double> ColumnWidths { get; set; } = [];
}
