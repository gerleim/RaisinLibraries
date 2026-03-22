namespace Raisin.EventSystem;

public class MessageArgs(string message) : EventSystemEventArgs
{
    public string Message { get; set; } = message;
    public MessageSeverity Severity { get; set; } = MessageSeverity.Info;
    public string? Category { get; set; }
    public string? Subcategory { get; set; }
}

public enum MessageSeverity
{
    Detail,
    Verbose,
    Info,
    Warning,
    Error,
    Critical
}
