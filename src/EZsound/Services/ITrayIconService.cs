using System.Windows.Forms;

namespace EZsound.Services;

public interface ITrayIconService : IDisposable
{
    event EventHandler? OpenRequested;
    event EventHandler? ToggleOverlayRequested;
    event EventHandler? ExitRequested;
    event EventHandler<bool>? AlwaysOnTopToggled;
    event EventHandler<bool>? VisualizerToggled;

    void SetAlwaysOnTopChecked(bool isChecked);
    void SetVisualizerChecked(bool isChecked);
    void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info);
}
