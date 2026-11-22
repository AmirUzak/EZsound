using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using EZsound.Models;
using EZsound.Services;

namespace EZsound.ViewModels;

/// <summary>
/// Main ViewModel orchestrating the application.
/// Manages the list of active audio sessions, master volume, and master audio wave capture.
/// Shared across MainWindow and OverlayWindow for seamless real-time synchronization.
/// </summary>
public class MainViewModel : BaseViewModel, IDisposable
{
    private readonly IAudioService _audioService;
    private readonly IAudioCaptureService _audioCapture;
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _refreshTimer;
    private bool _disposed;
    private string _statusText = "Ready";
    private bool _isVisualizerEnabled;

    public MainViewModel(AppSettings? settings = null)
    {
        _settings = settings ?? AppSettings.Load();
        _audioService = new AudioService();
        _audioCapture = new AudioCaptureService();
        MasterVolume = new MasterVolumeViewModel(_audioService);
        Sessions = new ObservableCollection<AudioSessionViewModel>();

        _isVisualizerEnabled = _settings.EnableVisualizer;
        if (_isVisualizerEnabled)
        {
            _audioCapture.Start();
        }

        CloseCommand = new RelayCommand(CloseApplication);
        ToggleVisualizerCommand = new RelayCommand(ToggleVisualizer);
        ToggleOverlayCommand = new RelayCommand(() => ToggleOverlayRequested?.Invoke(this, EventArgs.Empty));

        // Subscribe to audio service events
        _audioService.SessionsChanged += OnSessionsChanged;
        _audioService.SessionVolumeChanged += OnSessionVolumeChanged;
        _audioService.MasterVolumeChanged += OnMasterVolumeChanged;

        // Periodic refresh timer (every 2 seconds) serves as a background safety check
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _refreshTimer.Tick += (_, _) => RefreshSessions();
        _refreshTimer.Start();

        // Initial load
        RefreshSessions();
    }

    public ObservableCollection<AudioSessionViewModel> Sessions { get; }

    public MasterVolumeViewModel MasterVolume { get; }

    public IAudioCaptureService AudioCapture => _audioCapture;

    public bool IsVisualizerEnabled
    {
        get => _isVisualizerEnabled;
        set
        {
            if (SetProperty(ref _isVisualizerEnabled, value))
            {
                _settings.EnableVisualizer = value;
                _settings.Save();

                if (value)
                {
                    _audioCapture.Start();
                }
                else
                {
                    _audioCapture.Stop();
                }
            }
        }
    }

    public int SessionCount => Sessions.Count;

    public bool HasSessions => Sessions.Count > 0;

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public ICommand CloseCommand { get; }
    public ICommand ToggleVisualizerCommand { get; }
    public ICommand ToggleOverlayCommand { get; }

    public event EventHandler? ToggleOverlayRequested;

    private void ToggleVisualizer()
    {
        IsVisualizerEnabled = !IsVisualizerEnabled;
    }

    private void OnSessionsChanged(object? sender, EventArgs e)
    {
        // Marshal to UI thread Dispatcher
        System.Windows.Application.Current?.Dispatcher?.BeginInvoke(RefreshSessions);
    }

    private void OnSessionVolumeChanged(object? sender, (string SessionId, float Volume, bool IsMuted) e)
    {
        System.Windows.Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            var existing = Sessions.FirstOrDefault(vm => string.Equals(vm.SessionId, e.SessionId, StringComparison.OrdinalIgnoreCase));
            existing?.UpdateFromService(e.Volume, e.IsMuted);
        });
    }

    private void OnMasterVolumeChanged(object? sender, EventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            MasterVolume.RefreshFromService();
        });
    }

    /// <summary>
    /// Refreshes the session list, adding newly discovered sessions, removing expired ones,
    /// and updating process information/icons without recreating existing ViewModel instances.
    /// </summary>
    private void RefreshSessions()
    {
        try
        {
            var currentSessions = _audioService.GetAudioSessions();
            var currentIds = new HashSet<string>(currentSessions.Select(s => s.SessionId), StringComparer.OrdinalIgnoreCase);

            // Remove sessions that no longer exist
            var toRemove = Sessions.Where(vm => !currentIds.Contains(vm.SessionId)).ToList();
            foreach (var vm in toRemove)
            {
                Sessions.Remove(vm);
            }

            // Add new sessions and update existing ones
            foreach (var sessionData in currentSessions)
            {
                var existing = Sessions.FirstOrDefault(vm => string.Equals(vm.SessionId, sessionData.SessionId, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    // Update volume and mute from system
                    existing.UpdateFromService(sessionData.Volume, sessionData.IsMuted);

                    // Update metadata if it was delayed during process initialization
                    if (!string.IsNullOrEmpty(sessionData.DisplayName) &&
                        (string.IsNullOrEmpty(existing.DisplayName) || existing.DisplayName.StartsWith("Application (PID:") || existing.DisplayName.StartsWith("Unknown")))
                    {
                        existing.DisplayName = sessionData.DisplayName;
                    }

                    if (string.IsNullOrEmpty(existing.ExecutablePath) && !string.IsNullOrEmpty(sessionData.ExecutablePath))
                    {
                        existing.ExecutablePath = sessionData.ExecutablePath;
                    }

                    if (existing.ProcessId == 0 && sessionData.ProcessId != 0)
                    {
                        existing.ProcessId = sessionData.ProcessId;
                    }

                    if (string.IsNullOrEmpty(existing.ProcessName) && !string.IsNullOrEmpty(sessionData.ProcessName))
                    {
                        existing.ProcessName = sessionData.ProcessName;
                    }
                }
                else
                {
                    // Newly discovered session
                    var vm = new AudioSessionViewModel(_audioService)
                    {
                        SessionId = sessionData.SessionId,
                        DisplayName = sessionData.DisplayName,
                        ProcessName = sessionData.ProcessName,
                        ExecutablePath = sessionData.ExecutablePath,
                        ProcessId = sessionData.ProcessId,
                        IsSystemSound = sessionData.IsSystemSound,
                    };
                    vm.UpdateFromService(sessionData.Volume, sessionData.IsMuted);
                    Sessions.Add(vm);
                }
            }

            // Also refresh master volume state
            MasterVolume.RefreshFromService();

            // Update UI status
            OnPropertyChanged(nameof(SessionCount));
            OnPropertyChanged(nameof(HasSessions));
            StatusText = Sessions.Count > 0
                ? $"{Sessions.Count} active session{(Sessions.Count > 1 ? "s" : "")}"
                : "No active sessions";
        }
        catch (Exception ex)
        {
            App.Log($"[MainViewModel] Error refreshing sessions: {ex.Message}");
            Debug.WriteLine($"[EZsound] Error refreshing sessions: {ex.Message}");
            StatusText = "Error refreshing sessions";
        }
    }

    private void CloseApplication()
    {
        System.Windows.Application.Current?.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _refreshTimer.Stop();

        _audioService.SessionsChanged -= OnSessionsChanged;
        _audioService.SessionVolumeChanged -= OnSessionVolumeChanged;
        _audioService.MasterVolumeChanged -= OnMasterVolumeChanged;
        _audioService.Dispose();
        _audioCapture.Dispose();
    }
}
