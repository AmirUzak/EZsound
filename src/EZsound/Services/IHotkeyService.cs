namespace EZsound.Services;

public interface IHotkeyService : IDisposable
{
    event EventHandler? HotkeyPressed;
    bool IsRegistered { get; }
    string HotkeyDisplayString { get; }
    string? LastError { get; }
    bool Register(uint modifiers, uint key);
    void Unregister();
}
