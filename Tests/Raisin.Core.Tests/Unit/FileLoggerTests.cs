using FluentAssertions;
using Raisin.EventSystem;
using Xunit;

namespace Raisin.Core.Tests.Unit;

[Trait("Category", "Unit")]
public class FileLoggerTests : IDisposable
{
    // Inside a Raisin.Core.* namespace the bare name EventSystem binds to the
    // Raisin.EventSystem namespace, so the type has to be spelled out in full.
    private readonly Raisin.EventSystem.EventSystem _es = new();
    private readonly string _dir;
    private readonly string _basePath;

    public FileLoggerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"raisin-filelogger-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _basePath = Path.Combine(_dir, "test.log");
    }

    // FileLogger keeps the file open for writing, so a plain File.ReadAllText is
    // refused: the reader has to allow the writer's handle through FileShare.
    private string ReadLog()
    {
        var path = Path.Combine(_dir, $"test-{DateOnly.FromDateTime(DateTime.Now):yyyy-MM-dd}.log");
        if (!File.Exists(path)) return "";

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);
        return reader.ReadToEnd();
    }

    // ExecuteEvent is the subscriber entry point, called directly here so the tests do
    // not depend on EventSystem's thread-pool dispatch timing.
    private static LogArgs Entry(string message, LogSeverity severity)
        => new(message) { LogSeverity = severity };

    [Theory]
    [InlineData(LogSeverity.Warning)]
    [InlineData(LogSeverity.Error)]
    [InlineData(LogSeverity.Critical)]
    public void Warning_and_above_reach_the_file_without_waiting(LogSeverity severity)
    {
        using var logger = new FileLogger(_es, _basePath);

        logger.ExecuteEvent(this, Entry("urgent", severity));

        ReadLog().Should().Contain("urgent");
    }

    [Theory]
    [InlineData(LogSeverity.Detail)]
    [InlineData(LogSeverity.Verbose)]
    [InlineData(LogSeverity.Info)]
    public void Below_warning_is_buffered_rather_than_written_per_line(LogSeverity severity)
    {
        using var logger = new FileLogger(_es, _basePath);

        // Logged straight after construction, so the first timer tick is a second away.
        logger.ExecuteEvent(this, Entry("chatter", severity));

        ReadLog().Should().NotContain("chatter");
    }

    [Fact]
    public async Task Buffered_writes_are_flushed_by_the_timer()
    {
        using var logger = new FileLogger(_es, _basePath);
        logger.ExecuteEvent(this, Entry("chatter", LogSeverity.Verbose));

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline && !ReadLog().Contains("chatter"))
            await Task.Delay(50, TestContext.Current.CancellationToken);

        ReadLog().Should().Contain("chatter");
    }

    [Fact]
    public void Dispose_flushes_what_is_still_buffered()
    {
        var logger = new FileLogger(_es, _basePath);
        logger.ExecuteEvent(this, Entry("pending", LogSeverity.Verbose));
        ReadLog().Should().NotContain("pending");

        logger.Dispose();

        ReadLog().Should().Contain("pending");
    }

    [Fact]
    public void Logging_after_dispose_is_ignored_rather_than_throwing()
    {
        var logger = new FileLogger(_es, _basePath);
        logger.Dispose();

        var log = () => logger.ExecuteEvent(this, Entry("late", LogSeverity.Error));

        log.Should().NotThrow();
        ReadLog().Should().NotContain("late");
    }

    [Fact]
    public void Written_lines_carry_the_severity_and_the_source_type()
    {
        using var logger = new FileLogger(_es, _basePath);

        logger.ExecuteEvent(this, Entry("boom", LogSeverity.Error));

        ReadLog().Should().Contain("[Error]").And.Contain($"[{nameof(FileLoggerTests)}]");
    }

    public void Dispose()
    {
        _es.Dispose();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }
}
