namespace EZsound.Services;

public interface IWindowManagementService : IDisposable
{
    void ShowOverlay();
    void HideOverlay();
    void ToggleOverlay();
    void ShowMainWindow();
    void HideMainWindow();
    void ToggleMainWindow();
    void SetAlwaysOnTop(bool isAlwaysOnTop);
    void Shutdown();
}
