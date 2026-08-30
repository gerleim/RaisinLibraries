using System.Globalization;
using Raisin.EventSystem;

namespace Raisin.Core;

#pragma warning disable CS0618 // MessageArgs kept for backwards compatibility
public class FileLogger : IEventSubscriber<MessageArgs>, IEventSubscriber<LogArgs>, IDisposable
#pragma warning restore CS0618
{
    // Writes are buffered and flushed on a timer rather than per line: flushing every
    // line costs a write syscall while holding sync, which serialises every thread that
    // logs. Warning and above still flush immediately, so whatever explains a crash is
    // on disk before it happens.
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(1);

    private readonly string _logDirectory;
    private readonly string _baseName;
    private readonly string _extension;
    private readonly object sync = new();
    private readonly Timer _flushTimer;
    private StreamWriter _writer;
    private DateOnly _currentDate;
    private bool _pendingFlush;
    private bool _disposed;

    public FileLogger(Raisin.EventSystem.EventSystem es, string basePath, int retentionDays = 30)
    {
        _logDirectory = Path.GetDirectoryName(basePath) ?? ".";
        _extension = Path.GetExtension(basePath);
        _baseName = Path.GetFileNameWithoutExtension(basePath);

        if (!string.IsNullOrEmpty(_logDirectory))
            Directory.CreateDirectory(_logDirectory);

        _currentDate = DateOnly.FromDateTime(DateTime.Now);
        _writer = CreateWriter(_currentDate);
        _flushTimer = new Timer(_ => FlushPending(), null, FlushInterval, FlushInterval);

        CleanupOldLogs(retentionDays);
        es.SubscribeAll(this);
    }

    private string GetLogFilePath(DateOnly date)
        => Path.Combine(_logDirectory, $"{_baseName}-{date:yyyy-MM-dd}{_extension}");

    // AutoFlush stays off on every writer this class opens, including after a date rollover.
    private StreamWriter CreateWriter(DateOnly date)
        => new(GetLogFilePath(date), append: true) { AutoFlush = false };

    private void FlushPending()
    {
        lock (sync)
        {
            if (_disposed || !_pendingFlush) return;
            try
            {
                _writer.Flush();
                _pendingFlush = false;
            }
            catch (IOException) { }
        }
    }

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

#pragma warning disable CS0618 // MessageArgs kept for backwards compatibility
    public void ExecuteEvent(object sender, MessageArgs eventArgs)
        => WriteLine((LogSeverity)eventArgs.Severity, eventArgs.Message, sender?.GetType().Name);
#pragma warning restore CS0618

    public void ExecuteEvent(object sender, LogArgs eventArgs)
        => WriteLine(eventArgs.LogSeverity, eventArgs.Message, sender?.GetType().Name);

    private void WriteLine(LogSeverity severity, string message, string? source = null)
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
                    _writer = CreateWriter(today);
                    _pendingFlush = false;
                }
                _writer.WriteLine(line);

                if (severity >= LogSeverity.Warning)
                {
                    _writer.Flush();
                    _pendingFlush = false;
                }
                else
                {
                    _pendingFlush = true;
                }
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
        // Before the lock: the callback takes sync, and Timer.Dispose is idempotent.
        _flushTimer.Dispose();
        lock (sync)
        {
            if (_disposed) return;
            _disposed = true;
            _writer.Flush();
            _writer.Close();
        }
    }
}
