namespace Raisin.EventSystem;

public enum LogTarget
{
    File,
    UI
}

public enum LogSeverity
{
    Detail,
    Verbose,
    Info,
    Warning,
    Error,
    Critical
}

#pragma warning disable CS0618 // MessageSeverity kept for backwards compatibility
public class LogArgs(string message) : EventSystemEventArgs
{
    public string Message { get; set; } = message;

    [Obsolete("Use LogSeverity instead")]
    public MessageSeverity Severity
    {
        get
        {
            return (MessageSeverity)LogSeverity;
        }
        set
        {
            LogSeverity = (LogSeverity)value;
        }
    }

    public LogSeverity LogSeverity { get; set; } = LogSeverity.Info;

    public LogTarget Target { get; set; } = LogTarget.File;
    public string? Category { get; set; }
    public string? Subcategory { get; set; }
}
#pragma warning restore CS0618
