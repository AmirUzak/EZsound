namespace EZsound.Services;

/// <summary>
/// Service responsible for capturing the master audio output via WASAPI Loopback
/// to provide real-time audio amplitude for the Master Wave Visualizer.
/// </summary>
public interface IAudioCaptureService : IDisposable
{
    /// <summary>
    /// Gets the current normalized audio energy level (0.0 to 1.0).
    /// </summary>
    float CurrentLevel { get; }

    /// <summary>
    /// Gets whether audio capture is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Event fired when audio level is updated (~60 Hz).
    /// </summary>
    event EventHandler<float>? LevelUpdated;

    /// <summary>
    /// Starts capturing master audio output.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops capturing master audio output.
    /// </summary>
    void Stop();

    /// <summary>
    /// Restarts capture on the current default render device.
    /// </summary>
    void Restart();
}
