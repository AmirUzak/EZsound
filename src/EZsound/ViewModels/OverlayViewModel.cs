using System.Windows.Input;
using EZsound.Models;

namespace EZsound.ViewModels;

/// <summary>
/// ViewModel for the Floating Gaming Overlay window.
/// Reuses the existing MainViewModel so no audio services or polling are duplicated.
/// </summary>
public class OverlayViewModel : BaseViewModel
{
    private readonly MainViewModel _mainViewModel;
    private readonly AppSettings _settings;
    private bool _isAlwaysOnTop;
    private string _hotkeyText = "Ctrl + Shift + M";

    public event EventHandler? RequestHide;
    public event EventHandler<bool>? AlwaysOnTopChanged;

    public MainViewModel Main => _mainViewModel;

    public bool IsAlwaysOnTop
    {
        get => _isAlwaysOnTop;
        set
        {
            if (SetProperty(ref _isAlwaysOnTop, value))
            {
                OnPropertyChanged(nameof(PinIcon));
                OnPropertyChanged(nameof(PinTooltip));
                _settings.AlwaysOnTop = value;
                _settings.Save();
                AlwaysOnTopChanged?.Invoke(this, value);
            }
        }
    }

    public string PinIcon => _isAlwaysOnTop ? "\uE840" : "\uE718";
    public string PinTooltip => _isAlwaysOnTop ? "Always on Top: Enabled" : "Always on Top: Disabled";

    public string HotkeyText
    {
        get => _hotkeyText;
        set => SetProperty(ref _hotkeyText, value);
    }

    public ICommand ToggleAlwaysOnTopCommand { get; }
    public ICommand HideCommand { get; }

    public OverlayViewModel(MainViewModel mainViewModel, AppSettings settings)
    {
        _mainViewModel = mainViewModel;
        _settings = settings;
        _isAlwaysOnTop = settings.AlwaysOnTop;

        ToggleAlwaysOnTopCommand = new RelayCommand(() => IsAlwaysOnTop = !IsAlwaysOnTop);
        HideCommand = new RelayCommand(() => RequestHide?.Invoke(this, EventArgs.Empty));
    }
}
