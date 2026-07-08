namespace Raisin.WPF.Base.Models;

public interface IUndoRecord
{
    string? ContentId { get; }
    bool IsFloating { get; set; }
    double FloatingLeft { get; set; }
    double FloatingTop { get; set; }
    double FloatingWidth { get; set; }
    double FloatingHeight { get; set; }
}
