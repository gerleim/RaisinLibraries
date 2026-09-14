namespace Raisin.Core;

public class SessionEvent
{
    public DateTime Timestamp { get; init; }
    public string Type { get; init; } = "";
    public string? Detail { get; init; }
    public string? Account { get; init; }
    public double? NetLiquidation { get; init; }
    public double? PreviousDayEquity { get; init; }

    /// <summary>The broker's P&amp;L for the day so far, realized and unrealized, when the event was written.</summary>
    public double? DailyPnL { get; init; }
}
