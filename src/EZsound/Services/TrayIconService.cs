using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EZsound.Services;

public sealed class TrayIconService : ITrayIconService
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _alwaysOnTopMenuItem;
    private readonly ToolStripMenuItem _visualizerMenuItem;
    private readonly Icon _trayIcon;
    private bool _disposed;

    public event EventHandler? OpenRequested;
    public event EventHandler? ToggleOverlayRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler<bool>? AlwaysOnTopToggled;
    public event EventHandler<bool>? VisualizerToggled;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    public TrayIconService(bool initialAlwaysOnTop = true, bool initialEnableVisualizer = true)
    {
        _trayIcon = CreateAppIcon();

        var contextMenu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem("Open EZsound")
        {
            Font = new Font(Control.DefaultFont, FontStyle.Bold)
        };
        openItem.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);

        var toggleItem = new ToolStripMenuItem("Show / Hide Overlay (Ctrl+Shift+M)");
        toggleItem.Click += (_, _) => ToggleOverlayRequested?.Invoke(this, EventArgs.Empty);

        _alwaysOnTopMenuItem = new ToolStripMenuItem("Always on Top")
        {
            CheckOnClick = true,
            Checked = initialAlwaysOnTop
        };
        _alwaysOnTopMenuItem.CheckedChanged += (_, _) =>
            AlwaysOnTopToggled?.Invoke(this, _alwaysOnTopMenuItem.Checked);

        _visualizerMenuItem = new ToolStripMenuItem("Master Wave Visualizer")
        {
            CheckOnClick = true,
            Checked = initialEnableVisualizer
        };
        _visualizerMenuItem.CheckedChanged += (_, _) =>
            VisualizerToggled?.Invoke(this, _visualizerMenuItem.Checked);

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        contextMenu.Items.Add(openItem);
        contextMenu.Items.Add(toggleItem);
        contextMenu.Items.Add(_alwaysOnTopMenuItem);
        contextMenu.Items.Add(_visualizerMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = _trayIcon,
            Text = "EZsound - Volume Mixer",
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        // Left click toggles the overlay flyout (standard volume mixer behavior)
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ToggleOverlayRequested?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    public void SetAlwaysOnTopChecked(bool isChecked)
    {
        if (_alwaysOnTopMenuItem.Checked != isChecked)
        {
            _alwaysOnTopMenuItem.Checked = isChecked;
        }
    }

    public void SetVisualizerChecked(bool isChecked)
    {
        if (_visualizerMenuItem.Checked != isChecked)
        {
            _visualizerMenuItem.Checked = isChecked;
        }
    }

    public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon.ShowBalloonTip(3000, title, message, icon);
    }

    private static Icon CreateAppIcon()
    {
        try
        {
            // 1. Try to extract associated icon from current process executable
            string? exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath) && System.IO.File.Exists(exePath))
            {
                var associatedIcon = Icon.ExtractAssociatedIcon(exePath);
                if (associatedIcon != null)
                {
                    return associatedIcon;
                }
            }

            // 2. Try to load from file adjacent to executable
            string icoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
            if (System.IO.File.Exists(icoPath))
            {
                return new Icon(icoPath, 32, 32);
            }
        }
        catch (Exception ex)
        {
            // Expected: ExtractAssociatedIcon or icon loading may fail in unit test or headless environments.
            App.Log($"[TrayIconService] Could not load embedded icon, using fallback: {ex.Message}");
        }

        // Fallback procedural icon
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using (var bgBrush = new SolidBrush(Color.FromArgb(18, 21, 28)))
            {
                g.FillEllipse(bgBrush, 1, 1, 30, 30);
            }

            using (var pen = new Pen(Color.FromArgb(0, 210, 255), 2.5f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, 8, 16, 24, 16);
            }
        }

        IntPtr hIcon = bmp.GetHicon();
        Icon fallbackIcon = (Icon)Icon.FromHandle(hIcon).Clone();
        DestroyIcon(hIcon);
        return fallbackIcon;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _trayIcon.Dispose();
    }
}
