namespace Raisin.WPF.Base.Models;

public class DocumentState
{
    public string Type { get; set; } = "";
    public string ContentId { get; set; } = "";
    public int SelectedTabIndex { get; set; }
    public Dictionary<string, double> PanelHeights { get; set; } = [];
    public bool IsFloating { get; set; }
    public string FloatingTitle { get; set; } = "";
    public double FloatingLeft { get; set; }
    public double FloatingTop { get; set; }
    public double FloatingWidth { get; set; }
    public double FloatingHeight { get; set; }

    // Terminal session state
    public string WorkingDirectory { get; set; } = "";
    public string LastCommand { get; set; } = "";
    public bool WasInAlternateScreen { get; set; }
    public bool IsCommandRunning { get; set; }
    public string? ClaudeSessionName { get; set; }
}
