using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Raisin.Core;

namespace Raisin.WPF.Base;

public class SessionActivityService
{
    private readonly SessionEventLog _log;
    private readonly TimeSpan _idleThreshold;
    private DispatcherTimer? _idleTimer;
    private DispatcherTimer? _focusDebounce;
    private DateTime _deactivatedAtUtc;
    private Window? _window;
    private HwndSource? _hwndSource;
    private nint _powerNotifyHandle;
    private bool _isIdle;
    private bool _isFocused = true;
    private int _displayState = 1;

    public SessionActivityService(SessionEventLog log, TimeSpan? idleThreshold = null)
    {
        _log = log;
        _idleThreshold = idleThreshold ?? TimeSpan.FromMinutes(5);
    }

    public void Attach(Window window)
    {
        _window = window;
        _window.Activated += OnActivated;
        _window.Deactivated += OnDeactivated;

        var hwnd = new WindowInteropHelper(window).Handle;
        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(WndProc);

        var guid = NativeMethods.GUID_CONSOLE_DISPLAY_STATE;
        _powerNotifyHandle = NativeMethods.RegisterPowerSettingNotification(
            hwnd, ref guid, NativeMethods.DEVICE_NOTIFY_WINDOW_HANDLE);

        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _idleTimer.Tick += OnIdleTimerTick;
        _idleTimer.Start();
    }

    public void Detach()
    {
        _idleTimer?.Stop();
        _idleTimer = null;

        _focusDebounce?.Stop();
        _focusDebounce = null;

        if (_window is not null)
        {
            _window.Activated -= OnActivated;
            _window.Deactivated -= OnDeactivated;
            _window = null;
        }

        _hwndSource?.RemoveHook(WndProc);
        _hwndSource = null;

        if (_powerNotifyHandle != 0)
        {
            NativeMethods.UnregisterPowerSettingNotification(_powerNotifyHandle);
            _powerNotifyHandle = 0;
        }
    }

    private void OnActivated(object? sender, EventArgs e)
    {
        if (_focusDebounce is { IsEnabled: true })
        {
            _focusDebounce.Stop();
            return;
        }
        if (!_isFocused)
        {
            _isFocused = true;
            _log.AppendAsync(new SessionEvent { Timestamp = DateTime.UtcNow, Type = "app-focused" });
        }
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        _deactivatedAtUtc = DateTime.UtcNow;
        _focusDebounce ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _focusDebounce.Tick += OnFocusDebounceElapsed;
        _focusDebounce.Start();
    }

    private void OnFocusDebounceElapsed(object? sender, EventArgs e)
    {
        _focusDebounce!.Stop();
        _focusDebounce.Tick -= OnFocusDebounceElapsed;

        if (IsForegroundWindowOurs())
            return;

        _isFocused = false;
        _log.AppendAsync(new SessionEvent { Timestamp = _deactivatedAtUtc, Type = "app-unfocused" });
    }

    private static bool IsForegroundWindowOurs()
    {
        var fg = NativeMethods.GetForegroundWindow();
        if (fg == nint.Zero) return false;
        NativeMethods.GetWindowThreadProcessId(fg, out uint pid);
        return pid == (uint)Environment.ProcessId;
    }

    private void OnIdleTimerTick(object? sender, EventArgs e)
    {
        var idleTime = GetIdleTime();

        if (!_isIdle && idleTime >= _idleThreshold)
        {
            _isIdle = true;
            _log.AppendAsync(new SessionEvent
            {
                Timestamp = DateTime.UtcNow,
                Type = "user-idle",
                Detail = $"{_idleThreshold.TotalMinutes:0}min",
            });
        }
        else if (_isIdle && idleTime < _idleThreshold)
        {
            _isIdle = false;
            _log.AppendAsync(new SessionEvent { Timestamp = DateTime.UtcNow, Type = "user-active" });
        }
    }

    private static TimeSpan GetIdleTime()
    {
        var info = new NativeMethods.LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.LASTINPUTINFO>() };
        if (!NativeMethods.GetLastInputInfo(ref info))
            return TimeSpan.Zero;
        return TimeSpan.FromMilliseconds((uint)Environment.TickCount - info.dwTime);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_POWERBROADCAST && (int)wParam == NativeMethods.PBT_POWERSETTINGCHANGE)
        {
            var setting = Marshal.PtrToStructure<NativeMethods.POWERBROADCAST_SETTING>(lParam);
            if (setting.PowerSetting == NativeMethods.GUID_CONSOLE_DISPLAY_STATE)
            {
                int newState = setting.Data;
                if (newState != _displayState)
                {
                    _displayState = newState;
                    var type = newState switch
                    {
                        0 => "display-off",
                        1 => "display-on",
                        2 => "display-dimmed",
                        _ => null,
                    };
                    if (type is not null)
                        _log.AppendAsync(new SessionEvent { Timestamp = DateTime.UtcNow, Type = type });
                }
            }
        }
        return nint.Zero;
    }
}
