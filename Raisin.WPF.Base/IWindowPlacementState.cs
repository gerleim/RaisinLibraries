namespace Raisin.WPF.Base;

public interface IWindowPlacementState
{
    double? WindowLeft { get; set; }
    double? WindowTop { get; set; }
    double? WindowWidth { get; set; }
    double? WindowHeight { get; set; }
    bool WindowMaximized { get; set; }
}
