using System.Globalization;
using Raisin.EventSystem;

namespace Raisin.Core;

public class FileLogger : IEventSubscriber<MessageArgs>, IEventSubscriber<LogArgs>, IDisposable
{
    private readonly string _logDirectory;
    private readonly string _baseName;
    private readonly string _extension;
    private readonly object sync = new();
    private StreamWriter _writer;
    private DateOnly _currentDate;
    private bool _disposed;

    public FileLogger(Raisin.EventSystem.EventSystem es, string basePath, int retentionDays = 30)
    {
        _logDirectory = Path.GetDirectoryName(basePath) ?? ".";
        _extension = Path.GetExtension(basePath);
        _baseName = Path.GetFileNameWithoutExtension(basePath);

        if (!string.IsNullOrEmpty(_logDirectory))
            Directory.CreateDirectory(_logDirectory);

        _currentDate = DateOnly.FromDateTime(DateTime.Now);
        _writer = new StreamWriter(GetLogFilePath(_currentDate), append: true) { AutoFlush = true };

        CleanupOldLogs(retentionDays);
        es.SubscribeAll(this);
    }

    private string GetLogFilePath(DateOnly date)
        => Path.Combine(_logDirectory, $"{_baseName}-{date:yyyy-MM-dd}{_extension}");

    private void CleanupOldLogs(int retentionDays)
    {
        if (retentionDays <= 0) return;
        try
        {
            var cutoff = DateOnly.FromDateTime(DateTime.Now).AddDays(-retentionDays);
            var pattern = $"{_baseName}-*{_extension}";
            foreach (var file in Directory.GetFiles(_logDirectory, pattern))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var dateStr = fileName[((_baseName.Length + 1))..];
                if (DateOnly.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fileDate)
                    && fileDate < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch (IOException) { }
    }

    public void ExecuteEvent(object sender, MessageArgs eventArgs)
        => WriteLine(eventArgs.Severity, eventArgs.Message, sender?.GetType().Name);

    public void ExecuteEvent(object sender, LogArgs eventArgs)
        => WriteLine(eventArgs.Severity, eventArgs.Message, sender?.GetType().Name);

    private void WriteLine(MessageSeverity severity, string message, string? source = null)
    {
        try
        {
            var src = source is not null ? $" [{source}]" : "";
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{severity}]{src} {message}";
            lock (sync)
            {
                if (_disposed) return;
                var today = DateOnly.FromDateTime(DateTime.Now);
                if (today != _currentDate)
                {
                    _writer.Flush();
                    _writer.Close();
                    _currentDate = today;
                    _writer = new StreamWriter(GetLogFilePath(today), append: true) { AutoFlush = true };
                }
                _writer.WriteLine(line);
            }
        }
        catch (IOException) { }
    }

    public void DestroySubscriber()
    {
        Dispose();
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (_disposed) return;
            _disposed = true;
            _writer.Flush();
            _writer.Close();
        }
    }
}
