namespace Raisin.EventSystem;

public class LogArgs(string message) : EventSystemEventArgs
{
    public string Message { get; set; } = message;
    public MessageSeverity Severity { get; set; } = MessageSeverity.Info;
    public string? Category { get; set; }
    public string? Subcategory { get; set; }
}
