namespace EZsound.Models;

/// <summary>
/// Represents data about an audio session obtained from the Windows Audio Session API.
/// This is a plain data model — no COM references stored here.
/// </summary>
public class AudioSessionData
{
    public string SessionId { get; set; } = string.Empty;
    public uint ProcessId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public float Volume { get; set; } = 1.0f;
    public bool IsMuted { get; set; }
    public bool IsSystemSound { get; set; }
}
