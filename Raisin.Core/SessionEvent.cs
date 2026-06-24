namespace Raisin.Core;

public class SessionEvent
{
    public DateTime Timestamp { get; init; }
    public string Type { get; init; } = "";
    public string? Detail { get; init; }
    public string? Account { get; init; }
    public double? NetLiquidation { get; init; }
    public double? PreviousDayEquity { get; init; }
}
