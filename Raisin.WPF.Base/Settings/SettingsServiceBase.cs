using System.Text.Json;
using Raisin.Core;

namespace Raisin.WPF.Base.Settings;

public abstract class SettingsServiceBase<T> : DurableJsonStore<T> where T : new()
{
    private static event Action? _settingsChanged;
    public static event Action? SettingsChanged
    {
        add => _settingsChanged += value;
        remove => _settingsChanged -= value;
    }

    protected SettingsServiceBase(string filePath, JsonSerializerOptions? options = null)
        : base(filePath, options) { }

    public void SaveSettings(T settings)
    {
        Data = settings;
        WriteFile();
        _settingsChanged?.Invoke();
    }
}
