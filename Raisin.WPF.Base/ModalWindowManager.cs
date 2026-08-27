using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Raisin.WPF.Base;

public class ModalWindowManager : INotifyPropertyChanged
{
    private static ModalWindowManager? _instance;
    private object? _currentModalWindow;

    public static ModalWindowManager Instance => _instance ??= new ModalWindowManager();

    public bool IsCustomModalOpen => _currentModalWindow is not null;

    public object? CurrentModalWindow => _currentModalWindow;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void OpenModal(object window)
    {
        if (window is null) throw new ArgumentNullException(nameof(window));
        _currentModalWindow = window;
        OnPropertyChanged(nameof(IsCustomModalOpen));
        OnPropertyChanged(nameof(CurrentModalWindow));
    }

    public void CloseModal(object window)
    {
        if (window is null) throw new ArgumentNullException(nameof(window));
        if (_currentModalWindow == window)
        {
            _currentModalWindow = null;
            OnPropertyChanged(nameof(IsCustomModalOpen));
            OnPropertyChanged(nameof(CurrentModalWindow));
        }
    }

    public bool IsCurrentModal(object window) => _currentModalWindow == window;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
