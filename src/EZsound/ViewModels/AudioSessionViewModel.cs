using System.Windows.Input;
using System.Windows.Media;
using EZsound.Helpers;
using EZsound.Services;

namespace EZsound.ViewModels;

/// <summary>
/// ViewModel representing a single audio session (application).
/// </summary>
public class AudioSessionViewModel : BaseViewModel
{
    private readonly IAudioService _audioService;

    private string _sessionId = string.Empty;
    private string _displayName = string.Empty;
    private string _processName = string.Empty;
    private string _executablePath = string.Empty;
    private uint _processId;
    private float _volume = 1.0f;
    private bool _isMuted;
    private bool _isSystemSound;
    private ImageSource? _icon;
    private bool _isUpdatingFromService;

    public AudioSessionViewModel(IAudioService audioService)
    {
        _audioService = audioService;
        ToggleMuteCommand = new RelayCommand(ToggleMute);
    }

    public string SessionId
    {
        get => _sessionId;
        set => SetProperty(ref _sessionId, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string ProcessName
    {
        get => _processName;
        set => SetProperty(ref _processName, value);
    }

    public string ExecutablePath
    {
        get => _executablePath;
        set
        {
            if (SetProperty(ref _executablePath, value))
            {
                UpdateIcon();
            }
        }
    }

    public uint ProcessId
    {
        get => _processId;
        set
        {
            if (SetProperty(ref _processId, value))
            {
                UpdateIcon();
            }
        }
    }

    /// <summary>
    /// Volume as a percentage (0-100) for slider binding.
    /// </summary>
    public double VolumePercent
    {
        get => Math.Round(_volume * 100);
        set
        {
            var newVolume = (float)(value / 100.0);
            if (Math.Abs(_volume - newVolume) > 0.001f)
            {
                _volume = newVolume;
                OnPropertyChanged();
                OnPropertyChanged(nameof(VolumeText));

                if (!_isUpdatingFromService)
                {
                    _audioService.SetSessionVolume(_sessionId, _volume);
                }
            }
        }
    }

    public string VolumeText => $"{(int)VolumePercent}%";

    public float Volume
    {
        get => _volume;
        set
        {
            if (Math.Abs(_volume - value) > 0.001f)
            {
                _volume = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(VolumePercent));
                OnPropertyChanged(nameof(VolumeText));
            }
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (SetProperty(ref _isMuted, value))
            {
                OnPropertyChanged(nameof(MuteIcon));
                if (!_isUpdatingFromService)
                {
                    _audioService.SetSessionMute(_sessionId, _isMuted);
                }
            }
        }
    }

    /// <summary>
    /// Unicode icon for mute state display.
    /// </summary>
    public string MuteIcon => _isMuted ? "\uE74F" : "\uE767";

    public bool IsSystemSound
    {
        get => _isSystemSound;
        set => SetProperty(ref _isSystemSound, value);
    }

    public ImageSource? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public ICommand ToggleMuteCommand { get; }

    private void ToggleMute()
    {
        IsMuted = !IsMuted;
    }

    private void UpdateIcon()
    {
        Icon = IconHelper.GetIconForProcess(_executablePath, _processId);
    }

    /// <summary>
    /// Updates this ViewModel from fresh audio session data without pushing
    /// the values back to the audio service.
    /// </summary>
    public void UpdateFromService(float volume, bool isMuted)
    {
        _isUpdatingFromService = true;
        try
        {
            Volume = volume;
            IsMuted = isMuted;
        }
        finally
        {
            _isUpdatingFromService = false;
        }
    }
}
