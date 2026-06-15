namespace Raisin.Core;

public class SessionEvent
{
    public DateTime Timestamp { get; init; }
    public string Type { get; init; } = "";
    public string? Detail { get; init; }
}
