using System.Diagnostics;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using EZsound.Models;
using EZsound.Services;
using EZsound.ViewModels;

namespace EZsound;

public partial class App : System.Windows.Application
{
    private AppSettings? _settings;
    private MainViewModel? _mainViewModel;
    private OverlayViewModel? _overlayViewModel;
    private IHotkeyService? _hotkeyService;
    private ITrayIconService? _trayIconService;
    private IWindowManagementService? _windowManager;

    private static readonly string LogFile = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EZsound",
        "debug.log");

    public static void Log(string message)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(LogFile);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }
            System.IO.File.AppendAllText(LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Intentionally suppressed: failure in logging must never crash the host application.
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Log("=== OnStartup started ===");

        // Prevent app from closing when all windows are hidden into tray
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Global unhandled exception handlers
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        try
        {
            // 1. Load Settings
            Log("1. Loading settings...");
            _settings = AppSettings.Load();
            Log($"Settings loaded: StartMinimized={_settings.StartMinimized}, AlwaysOnTop={_settings.AlwaysOnTop}, EnableVisualizer={_settings.EnableVisualizer}, OverlayLeft={_settings.OverlayLeft}, OverlayTop={_settings.OverlayTop}");

            // 2. Initialize Core Audio & ViewModels (Shared single instances)
            Log("2. Initializing MainViewModel...");
            _mainViewModel = new MainViewModel(_settings);
            Log("2.1 Initializing OverlayViewModel...");
            _overlayViewModel = new OverlayViewModel(_mainViewModel, _settings);

            // 3. Initialize Window Manager
            Log("3. Initializing WindowManagementService...");
            _windowManager = new WindowManagementService(_settings, _mainViewModel, _overlayViewModel);
            _mainViewModel.ToggleOverlayRequested += (_, _) =>
            {
                Log("MainViewModel ToggleOverlayRequested -> ToggleOverlay");
                Dispatcher.BeginInvoke(() => _windowManager.ToggleOverlay());
            };

            // 4. Initialize Hotkey Service
            Log("4. Initializing HotkeyService...");
            _hotkeyService = new HotkeyService();
            _hotkeyService.HotkeyPressed += (_, _) =>
            {
                Log("Hotkey pressed! Toggling overlay.");
                Dispatcher.BeginInvoke(() => _windowManager.ToggleOverlay());
            };

            bool hotkeyRegistered = _hotkeyService.Register(_settings.HotkeyModifiers, _settings.HotkeyKey);
            _overlayViewModel.HotkeyText = _hotkeyService.HotkeyDisplayString;
            Log($"Hotkey registered={hotkeyRegistered}, display={_hotkeyService.HotkeyDisplayString}");

            // 5. Initialize Tray Icon Service
            Log("5. Initializing TrayIconService...");
            _trayIconService = new TrayIconService(_settings.AlwaysOnTop, _settings.EnableVisualizer);
            _trayIconService.OpenRequested += (_, _) =>
            {
                Log("Tray OpenRequested -> ShowMainWindow");
                Dispatcher.BeginInvoke(() => _windowManager.ShowMainWindow());
            };
            _trayIconService.ToggleOverlayRequested += (_, _) =>
            {
                Log("Tray ToggleOverlayRequested -> ToggleOverlay");
                Dispatcher.BeginInvoke(() => _windowManager.ToggleOverlay());
            };
            _trayIconService.AlwaysOnTopToggled += (_, isTop) =>
            {
                Log($"Tray AlwaysOnTopToggled: {isTop}");
                Dispatcher.BeginInvoke(() => _windowManager.SetAlwaysOnTop(isTop));
            };
            _trayIconService.VisualizerToggled += (_, isEnabled) =>
            {
                Log($"Tray VisualizerToggled: {isEnabled}");
                Dispatcher.BeginInvoke(() => _mainViewModel.IsVisualizerEnabled = isEnabled);
            };
            _trayIconService.ExitRequested += (_, _) =>
            {
                Log("Tray ExitRequested -> Shutdown");
                Dispatcher.BeginInvoke(() => _windowManager.Shutdown());
            };

            // Notify user of hotkey status
            if (!hotkeyRegistered && _hotkeyService.LastError != null)
            {
                Log($"Showing hotkey warning notification: {_hotkeyService.LastError}");
                _trayIconService.ShowNotification(
                    "EZsound Hotkey Notice",
                    _hotkeyService.LastError,
                    ToolTipIcon.Warning);
            }

            // 6. Launch view according to settings
            Log($"6. Launch view. StartMinimized={_settings.StartMinimized}");
            if (!_settings.StartMinimized)
            {
                Log("Calling ShowMainWindow()...");
                _windowManager.ShowMainWindow();
                Log("ShowMainWindow() returned.");
            }
        }
        catch (Exception ex)
        {
            Log($"FATAL OnStartup exception: {ex}");
            Debug.WriteLine($"[EZsound] Fatal startup exception: {ex}");
            System.Windows.MessageBox.Show(
                $"Failed to start EZsound:\n{ex.Message}",
                "EZsound Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log($"=== OnExit called with ExitCode={e.ApplicationExitCode} ===");
        _hotkeyService?.Dispose();
        _trayIconService?.Dispose();
        _windowManager?.Dispose();
        _mainViewModel?.Dispose();
        _settings?.Save();

        base.OnExit(e);
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log($"[App_DispatcherUnhandledException] {e.Exception}");
        Debug.WriteLine($"[EZsound] Unhandled UI exception: {e.Exception}");
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log($"[CurrentDomain_UnhandledException] IsTerminating={e.IsTerminating}, {e.ExceptionObject}");
        Debug.WriteLine($"[EZsound] Unhandled domain exception: {e.ExceptionObject}");
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log($"[TaskScheduler_UnobservedTaskException] {e.Exception}");
        Debug.WriteLine($"[EZsound] Unobserved task exception: {e.Exception}");
        e.SetObserved();
    }
}
