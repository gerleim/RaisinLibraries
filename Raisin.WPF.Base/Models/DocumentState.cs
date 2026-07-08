namespace Raisin.WPF.Base.Models;

public class DocumentState : IUndoRecord
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

    string? IUndoRecord.ContentId => ContentId;
}
