using System.Text.Json;
using System.Text.Json.Serialization;

namespace Raisin.Core;

public class SessionEventLog
{
    public const string FileName = "session-events.jsonl";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    private readonly string _filePath;
    private readonly object _sync = new();
    private readonly List<SessionEvent> _events = [];

    public SessionEventLog(string filePath)
    {
        _filePath = filePath;
        LoadExisting();
    }

    public Task AppendAsync(SessionEvent e)
    {
        return Task.Run(() =>
        {
            try
            {
                lock (_sync)
                {
                    _events.Add(e);
                    AppendLine(e);
                }
            }
            catch { }
        });
    }

    public IReadOnlyList<SessionEvent> GetAll()
    {
        lock (_sync)
            return _events.ToList();
    }

    private void AppendLine(SessionEvent e)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var sw = new StreamWriter(_filePath, append: true);
        sw.WriteLine(JsonSerializer.Serialize(e, JsonOptions));
    }

    private void LoadExisting()
    {
        if (!File.Exists(_filePath)) return;

        try
        {
            foreach (var line in File.ReadLines(_filePath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var e = JsonSerializer.Deserialize<SessionEvent>(line, JsonOptions);
                    if (e is not null)
                        _events.Add(e);
                }
                catch { }
            }
        }
        catch { }
    }
}
