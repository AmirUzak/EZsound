using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace EZsound.Models;

/// <summary>
/// Application settings persisted to disk.
/// </summary>
public class AppSettings
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EZsound");

    private static readonly string SettingsFilePath = Path.Combine(SettingsDir, "settings.json");

    public bool AlwaysOnTop { get; set; } = true;
    public bool HideToTrayOnClose { get; set; } = true;
    public bool StartMinimized { get; set; } = false;
    public bool EnableVisualizer { get; set; } = true;
    public double? OverlayLeft { get; set; }
    public double? OverlayTop { get; set; }

    // Hotkey: Default is Ctrl + Shift + M
    // 0x0002 = MOD_CONTROL, 0x0004 = MOD_SHIFT
    public uint HotkeyModifiers { get; set; } = 0x0002 | 0x0004;
    // 0x4D = 'M'
    public uint HotkeyKey { get; set; } = (uint)Keys.M;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            App.Log($"[AppSettings] Error loading settings: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[EZsound] Error loading settings: {ex.Message}");
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            if (!Directory.Exists(SettingsDir))
            {
                Directory.CreateDirectory(SettingsDir);
            }

            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            App.Log($"[AppSettings] Error saving settings: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[EZsound] Error saving settings: {ex.Message}");
        }
    }
}
