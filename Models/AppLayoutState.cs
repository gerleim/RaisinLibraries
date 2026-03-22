namespace Raisin.WPF.Base.Models;

public record FloatingWindowBounds(double Left, double Top, double Width, double Height);

public class AppLayoutState
{
    public int Version { get; set; } = 1;
    public List<DocumentState> Documents { get; set; } = [];
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }
    public Dictionary<string, FloatingWindowBounds> FloatingDefaults { get; set; } = [];
}
