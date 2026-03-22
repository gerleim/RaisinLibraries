namespace Raisin.EventSystem;

public enum AlertType { ScannerNewSymbol, PriceAlert, System }

public class AlertArgs(AlertType type, string message) : EventSystemEventArgs
{
    public AlertType Type { get; } = type;
    public string Message { get; } = message;
    public string? Symbol { get; init; }
    public string Category { get; init; } = "";
}
