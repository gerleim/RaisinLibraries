using System.Windows;

namespace Raisin.StyleInspector;

public static class StyleInspector
{
    private static Application? _app;
    private static InspectorWindow? _window;

    public static void Enable(Application app)
    {
        _app = app;
    }

    public static void Open()
    {
        if (_app == null)
            return;

        if (_window == null || !_window.IsLoaded)
        {
            _window = new InspectorWindow
            {
                Owner = _app.MainWindow
            };
        }

        _window.Show();
        _window.Activate();
    }

    public static void Close()
    {
        _window?.Close();
        _window = null;
    }

    public static void Toggle()
    {
        if (_window is { IsLoaded: true, IsVisible: true })
            Close();
        else
            Open();
    }
}
