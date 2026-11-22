using System.Windows;
using EZsound.Models;
using EZsound.ViewModels;
using EZsound.Views;

namespace EZsound.Services;

public sealed class WindowManagementService : IWindowManagementService
{
    private readonly AppSettings _settings;
    private readonly MainViewModel _mainViewModel;
    private readonly OverlayViewModel _overlayViewModel;
    private readonly OverlayWindow _overlayWindow;
    private readonly System.Windows.Threading.DispatcherTimer _saveDebounceTimer;
    private MainWindow? _mainWindow;
    private bool _disposed;

    public WindowManagementService(
        AppSettings settings,
        MainViewModel mainViewModel,
        OverlayViewModel overlayViewModel)
    {
        _settings = settings;
        _mainViewModel = mainViewModel;
        _overlayViewModel = overlayViewModel;

        _saveDebounceTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _saveDebounceTimer.Tick += (_, _) =>
        {
            _saveDebounceTimer.Stop();
            _settings.Save();
        };

        _overlayWindow = new OverlayWindow(_overlayViewModel);
        _overlayWindow.PositionChanged += OverlayWindow_PositionChanged;

        RestoreOverlayPosition();
    }

    public void ShowOverlay()
    {
        EnsureOverlayPositionValid();
        _overlayWindow.SmoothShow();
    }

    public void HideOverlay()
    {
        _saveDebounceTimer.Stop();
        _settings.OverlayLeft = _overlayWindow.Left;
        _settings.OverlayTop = _overlayWindow.Top;
        _settings.Save();
        _overlayWindow.SmoothHide();
    }

    public void ToggleOverlay()
    {
        if (_overlayWindow.Visibility == Visibility.Visible && _overlayWindow.IsActive)
        {
            HideOverlay();
        }
        else
        {
            ShowOverlay();
        }
    }

    public void ShowMainWindow()
    {
        if (_mainWindow == null)
        {
            _mainWindow = new MainWindow(_mainViewModel, _settings);
            _mainWindow.Closed += (_, _) => _mainWindow = null;
        }

        _mainWindow.Topmost = _settings.AlwaysOnTop;
        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }
        _mainWindow.Activate();
    }

    public void HideMainWindow()
    {
        _mainWindow?.Hide();
    }

    public void ToggleMainWindow()
    {
        if (_mainWindow != null && _mainWindow.Visibility == Visibility.Visible && _mainWindow.IsActive)
        {
            HideMainWindow();
        }
        else
        {
            ShowMainWindow();
        }
    }

    public void SetAlwaysOnTop(bool isAlwaysOnTop)
    {
        _settings.AlwaysOnTop = isAlwaysOnTop;
        _settings.Save();

        _overlayViewModel.IsAlwaysOnTop = isAlwaysOnTop;
        _overlayWindow.Topmost = isAlwaysOnTop;

        if (_mainWindow != null)
        {
            _mainWindow.Topmost = isAlwaysOnTop;
        }
    }

    private void RestoreOverlayPosition()
    {
        if (_settings.OverlayLeft.HasValue && _settings.OverlayTop.HasValue)
        {
            _overlayWindow.Left = _settings.OverlayLeft.Value;
            _overlayWindow.Top = _settings.OverlayTop.Value;
        }
        else
        {
            // Position on right side of primary work area
            var workArea = SystemParameters.WorkArea;
            _overlayWindow.Left = Math.Max(20, workArea.Right - 420);
            _overlayWindow.Top = Math.Max(40, workArea.Top + 60);
        }

        EnsureOverlayPositionValid();
    }

    private void EnsureOverlayPositionValid()
    {
        // Guard against off-screen positions if monitor configuration changed
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualWidth = SystemParameters.VirtualScreenWidth;
        var virtualHeight = SystemParameters.VirtualScreenHeight;

        if (_overlayWindow.Left < virtualLeft || _overlayWindow.Left > virtualLeft + virtualWidth - 100 ||
            _overlayWindow.Top < virtualTop || _overlayWindow.Top > virtualTop + virtualHeight - 100)
        {
            var workArea = SystemParameters.WorkArea;
            _overlayWindow.Left = Math.Max(20, workArea.Right - 420);
            _overlayWindow.Top = Math.Max(40, workArea.Top + 60);
        }
    }

    private void OverlayWindow_PositionChanged(object? sender, EventArgs e)
    {
        _settings.OverlayLeft = _overlayWindow.Left;
        _settings.OverlayTop = _overlayWindow.Top;

        // Debounce disk writes while the user is actively dragging the window
        _saveDebounceTimer.Stop();
        _saveDebounceTimer.Start();
    }

    public void Shutdown()
    {
        _saveDebounceTimer.Stop();
        _settings.Save();
        _overlayWindow.ForceClose();
        _mainWindow?.ForceClose();

        System.Windows.Application.Current?.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _saveDebounceTimer.Stop();
        _overlayWindow.PositionChanged -= OverlayWindow_PositionChanged;
        _overlayWindow.ForceClose();
        _mainWindow?.ForceClose();
    }
}
