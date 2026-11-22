using System.Windows.Input;
using EZsound.Services;

namespace EZsound.ViewModels;

/// <summary>
/// ViewModel for the Master Volume control.
/// </summary>
public class MasterVolumeViewModel : BaseViewModel
{
    private readonly IAudioService _audioService;
    private float _volume = 1.0f;
    private bool _isMuted;
    private bool _isUpdatingFromService;

    public MasterVolumeViewModel(IAudioService audioService)
    {
        _audioService = audioService;
        ToggleMuteCommand = new RelayCommand(ToggleMute);

        // Load initial values
        RefreshFromService();
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
                    _audioService.SetMasterVolume(_volume);
                }
            }
        }
    }

    public string VolumeText => $"{(int)VolumePercent}%";

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
                    _audioService.SetMasterMute(_isMuted);
                }
            }
        }
    }

    public string MuteIcon => _isMuted ? "\uE74F" : "\uE767";

    public ICommand ToggleMuteCommand { get; }

    private void ToggleMute()
    {
        IsMuted = !IsMuted;
    }

    public void RefreshFromService()
    {
        _isUpdatingFromService = true;
        try
        {
            var vol = _audioService.GetMasterVolume();
            _volume = vol;
            OnPropertyChanged(nameof(VolumePercent));
            OnPropertyChanged(nameof(VolumeText));

            var muted = _audioService.GetMasterMute();
            _isMuted = muted;
            OnPropertyChanged(nameof(IsMuted));
            OnPropertyChanged(nameof(MuteIcon));
        }
        finally
        {
            _isUpdatingFromService = false;
        }
    }
}
