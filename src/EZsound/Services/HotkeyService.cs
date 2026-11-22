using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace EZsound.Services;

public sealed class HotkeyService : IHotkeyService
{
    private const int HotkeyId = 9001;
    private const int WmHotkey = 0x0312;

    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;
    public const uint ModNoRepeat = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly HotkeyWindow _window;
    private bool _isRegistered;
    private uint _currentModifiers;
    private uint _currentKey;
    private string? _lastError;
    private bool _disposed;

    public event EventHandler? HotkeyPressed;

    public bool IsRegistered => _isRegistered;
    public string? LastError => _lastError;
    public string HotkeyDisplayString => FormatHotkeyString(_currentModifiers, _currentKey);

    public HotkeyService()
    {
        _window = new HotkeyWindow(OnHotkeyPressed);
    }

    public bool Register(uint modifiers, uint key)
    {
        Unregister();

        _currentModifiers = modifiers;
        _currentKey = key;

        // Add MOD_NOREPEAT to prevent multiple triggers if key is held down
        uint flags = modifiers | ModNoRepeat;

        bool success = RegisterHotKey(_window.Handle, HotkeyId, flags, key);
        if (!success)
        {
            // Try without MOD_NOREPEAT in case of compatibility
            success = RegisterHotKey(_window.Handle, HotkeyId, modifiers, key);
        }

        if (success)
        {
            _isRegistered = true;
            _lastError = null;
            Debug.WriteLine($"[EZsound] Hotkey registered: {HotkeyDisplayString}");
            return true;
        }

        int errorCode = Marshal.GetLastWin32Error();
        _isRegistered = false;
        _lastError = errorCode == 1409
            ? $"Hotkey {HotkeyDisplayString} is already registered by another application."
            : $"Failed to register hotkey {HotkeyDisplayString} (Error {errorCode}).";

        Debug.WriteLine($"[EZsound] {_lastError}");
        return false;
    }

    public void Unregister()
    {
        if (_isRegistered)
        {
            UnregisterHotKey(_window.Handle, HotkeyId);
            _isRegistered = false;
            Debug.WriteLine("[EZsound] Hotkey unregistered.");
        }
    }

    private void OnHotkeyPressed()
    {
        Debug.WriteLine("[EZsound] Global hotkey pressed!");
        HotkeyPressed?.Invoke(this, EventArgs.Empty);
    }

    private static string FormatHotkeyString(uint modifiers, uint key)
    {
        var sb = new StringBuilder();
        if ((modifiers & ModControl) != 0) sb.Append("Ctrl + ");
        if ((modifiers & ModShift) != 0) sb.Append("Shift + ");
        if ((modifiers & ModAlt) != 0) sb.Append("Alt + ");
        if ((modifiers & ModWin) != 0) sb.Append("Win + ");

        var keyName = ((Keys)key).ToString();
        sb.Append(keyName);

        return sb.ToString();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Unregister();
        _window.Dispose();
    }

    private sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        private readonly Action _onHotkeyPressed;

        public HotkeyWindow(Action onHotkeyPressed)
        {
            _onHotkeyPressed = onHotkeyPressed;
            CreateHandle(new CreateParams
            {
                Caption = "EZsound_HotkeyListener"
            });
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId)
            {
                _onHotkeyPressed();
            }

            base.WndProc(ref m);
        }

        public void Dispose()
        {
            DestroyHandle();
        }
    }
}
