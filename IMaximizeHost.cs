using System.Windows;

namespace Raisin.WPF.Base;

public interface IMaximizeHost
{
    bool IsAnyViewMaximized { get; }
    bool MaximizeView(ToolWindowViewModel vm, FrameworkElement view);
    void RestoreMaximizedView();
}
