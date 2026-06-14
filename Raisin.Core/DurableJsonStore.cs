using System.Text.Json;

namespace Raisin.Core;

public abstract class DurableJsonStore<T> : DurableFileStore where T : new()
{
    protected T Data;
    private readonly JsonSerializerOptions? _jsonOptions;

    protected DurableJsonStore(string filePath, JsonSerializerOptions? jsonOptions = null) : base(filePath)
    {
        Data = new T();
        _jsonOptions = jsonOptions;
    }

    protected sealed override void ReadFile()
    {
        try
        {
            var json = File.ReadAllText(FilePath);
            Data = JsonSerializer.Deserialize<T>(json, _jsonOptions) ?? new T();
        }
        catch
        {
            Data = new T();
        }
    }

    protected sealed override void WriteFile()
    {
        try
        {
            var json = JsonSerializer.Serialize(Data, _jsonOptions);
            SafeFile.WriteAllText(FilePath, json);
        }
        catch (Exception ex)
        {
            OnWriteError(ex);
        }
    }

    protected virtual void OnWriteError(Exception ex) { }
}
