using EZsound.Models;

namespace EZsound.Services;

/// <summary>
/// Interface for the audio session management service.
/// </summary>
public interface IAudioService : IDisposable
{
    /// <summary>
    /// Gets all current audio sessions.
    /// </summary>
    List<AudioSessionData> GetAudioSessions();

    /// <summary>
    /// Sets the volume of a specific audio session.
    /// </summary>
    /// <param name="sessionId">Session identifier.</param>
    /// <param name="volume">Volume level from 0.0 to 1.0.</param>
    void SetSessionVolume(string sessionId, float volume);

    /// <summary>
    /// Gets the volume of a specific audio session.
    /// </summary>
    float GetSessionVolume(string sessionId);

    /// <summary>
    /// Sets the mute state of a specific audio session.
    /// </summary>
    void SetSessionMute(string sessionId, bool muted);

    /// <summary>
    /// Gets the mute state of a specific audio session.
    /// </summary>
    bool GetSessionMute(string sessionId);

    /// <summary>
    /// Gets the master volume level (0.0 to 1.0).
    /// </summary>
    float GetMasterVolume();

    /// <summary>
    /// Sets the master volume level (0.0 to 1.0).
    /// </summary>
    void SetMasterVolume(float volume);

    /// <summary>
    /// Gets the master mute state.
    /// </summary>
    bool GetMasterMute();

    /// <summary>
    /// Sets the master mute state.
    /// </summary>
    void SetMasterMute(bool muted);

    /// <summary>
    /// Event fired when audio sessions change (session added/removed).
    /// </summary>
    event EventHandler? SessionsChanged;

    /// <summary>
    /// Event fired when an individual session's volume or mute state changes externally.
    /// </summary>
    event EventHandler<(string SessionId, float Volume, bool IsMuted)>? SessionVolumeChanged;

    /// <summary>
    /// Event fired when master volume or mute changes externally.
    /// </summary>
    event EventHandler? MasterVolumeChanged;
}
