using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using EZsound.Helpers;
using EZsound.Models;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace EZsound.Services;

/// <summary>
/// Audio service implementation using NAudio and Windows Core Audio API (WASAPI).
/// Provides real-time detection of new and expired audio sessions, per-app volume/mute control,
/// and master volume management.
/// </summary>
public sealed class AudioService : IAudioService
{
    private readonly object _lock = new();
    private MMDeviceEnumerator? _deviceEnumerator;
    private MMDevice? _defaultDevice;
    private AudioSessionManager? _sessionManager;
    private readonly Dictionary<string, AudioSessionControl> _sessionControls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SessionEventClient> _sessionEventClients = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;
    private readonly DeviceNotificationClient _deviceNotification;

    public event EventHandler? SessionsChanged;
    public event EventHandler<(string SessionId, float Volume, bool IsMuted)>? SessionVolumeChanged;
    public event EventHandler? MasterVolumeChanged;

    public AudioService()
    {
        _deviceNotification = new DeviceNotificationClient(this);
        InitializeDevice();
    }

    private void InitializeDevice()
    {
        lock (_lock)
        {
            CleanupDevice();

            try
            {
                _deviceEnumerator = new MMDeviceEnumerator();
                _deviceEnumerator.RegisterEndpointNotificationCallback(_deviceNotification);

                try
                {
                    _defaultDevice = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                }
                catch (COMException)
                {
                    Debug.WriteLine("[EZsound] No default audio render device found.");
                    return;
                }

                _sessionManager = _defaultDevice.AudioSessionManager;
                if (_sessionManager != null)
                {
                    // Register for new session creation notifications (IAudioSessionNotification)
                    _sessionManager.OnSessionCreated += SessionManager_OnSessionCreated;
                    RefreshSessionControls();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EZsound] Error initializing audio device: {ex.Message}");
            }
        }
    }

    private void CleanupDevice()
    {
        // Unsubscribe all session event clients
        foreach (var kvp in _sessionControls)
        {
            if (_sessionEventClients.TryGetValue(kvp.Key, out var client))
            {
                try
                {
                    kvp.Value.UnRegisterEventClient(client);
                }
                catch
                {
                    // Expected: COM session may already be dead or disconnected during device cleanup.
                }
            }
        }
        _sessionControls.Clear();
        _sessionEventClients.Clear();

        if (_sessionManager != null)
        {
            try
            {
                _sessionManager.OnSessionCreated -= SessionManager_OnSessionCreated;
            }
            catch
            {
                // Expected: Session manager may already be disposed or invalidated.
            }

            try
            {
                _sessionManager.Dispose();
            }
            catch
            {
                // Expected: COM release may throw if device was unplugged.
            }

            _sessionManager = null;
        }

        if (_defaultDevice != null)
        {
            try
            {
                _defaultDevice.Dispose();
            }
            catch
            {
                // Expected: Device may already be invalidated or disconnected.
            }
            _defaultDevice = null;
        }
    }

    /// <summary>
    /// Synchronizes the session collection with WASAPI.
    /// Preserves existing registered event clients to avoid event drops while detecting newly added/removed sessions.
    /// </summary>
    private void RefreshSessionControls()
    {
        lock (_lock)
        {
            if (_sessionManager == null) return;

            try
            {
                // CRITICAL: Call RefreshSessions() on NAudio's AudioSessionManager.
                // NAudio caches the SessionCollection internally; calling RefreshSessions() forces it
                // to re-query IAudioSessionEnumerator from Windows Core Audio API.
                _sessionManager.RefreshSessions();
                var sessions = _sessionManager.Sessions;
                if (sessions == null) return;

                var discoveredIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < sessions.Count; i++)
                {
                    try
                    {
                        var session = sessions[i];
                        var sessionId = GetSessionIdentifier(session);
                        if (string.IsNullOrEmpty(sessionId)) continue;

                        discoveredIds.Add(sessionId);

                        // If not tracked yet, register our dedicated session event client
                        if (!_sessionControls.ContainsKey(sessionId))
                        {
                            _sessionControls[sessionId] = session;
                            var client = new SessionEventClient(this, sessionId, session);
                            _sessionEventClients[sessionId] = client;

                            try
                            {
                                session.RegisterEventClient(client);
                                Debug.WriteLine($"[EZsound] Registered event client for session: {sessionId}");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[EZsound] Failed to register event client for session {sessionId}: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[EZsound] Error processing session {i}: {ex.Message}");
                    }
                }

                // Clean up sessions that are no longer reported by the enumerator
                var deadIds = _sessionControls.Keys.Where(id => !discoveredIds.Contains(id)).ToList();
                foreach (var deadId in deadIds)
                {
                    if (_sessionControls.TryGetValue(deadId, out var deadSession))
                    {
                        if (_sessionEventClients.TryGetValue(deadId, out var client))
                        {
                            try 
                            { 
                                deadSession.UnRegisterEventClient(client); 
                            } 
                            catch 
                            { 
                                // Expected: COM session may already be dead or disconnected.
                            }
                            _sessionEventClients.Remove(deadId);
                        }
                        _sessionControls.Remove(deadId);
                        Debug.WriteLine($"[EZsound] Removed expired/closed session: {deadId}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EZsound] Error enumerating sessions: {ex.Message}");
            }
        }
    }

    private static string GetSessionIdentifier(AudioSessionControl session)
    {
        try
        {
            var id = session.GetSessionInstanceIdentifier;
            if (!string.IsNullOrEmpty(id)) return id;
        }
        catch
        {
            // Expected: COM session may be invalid or terminating during identifier retrieval.
        }

        try
        {
            var id = session.GetSessionIdentifier;
            if (!string.IsNullOrEmpty(id)) return id;
        }
        catch
        {
            // Expected: COM session may not support identifier interface or is closing.
        }

        try
        {
            var pid = session.GetProcessID;
            if (pid != 0) return $"PID_{pid}_{session.GetHashCode()}";
        }
        catch
        {
            // Expected: COM session process ID retrieval may fail if process already exited.
        }

        return string.Empty;
    }

    public List<AudioSessionData> GetAudioSessions()
    {
        var result = new List<AudioSessionData>();

        lock (_lock)
        {
            if (_sessionManager == null) return result;

            // Re-enumerate from WASAPI
            RefreshSessionControls();

            var currentPid = (uint)Environment.ProcessId;
            var toRemove = new List<string>();

            foreach (var kvp in _sessionControls)
            {
                try
                {
                    var session = kvp.Value;
                    var state = session.State;

                    // Skip expired sessions
                    if (state == AudioSessionState.AudioSessionStateExpired)
                    {
                        toRemove.Add(kvp.Key);
                        continue;
                    }

                    var pid = GetProcessId(session);

                    // Skip EZsound itself
                    if (pid == currentPid && pid != 0)
                        continue;

                    var data = new AudioSessionData
                    {
                        SessionId = kvp.Key,
                        ProcessId = pid,
                        Volume = GetSimpleVolume(session),
                        IsMuted = GetMuteState(session),
                        IsSystemSound = session.IsSystemSoundsSession
                    };

                    // Resolve Process Name and Executable Path using safe ProcessHelper
                    if (pid != 0)
                    {
                        data.ExecutablePath = ProcessHelper.GetExecutablePath(pid);
                        data.ProcessName = ProcessHelper.GetProcessName(pid, data.ExecutablePath);
                    }

                    // Try to get DisplayName from session
                    try
                    {
                        var displayName = session.DisplayName;
                        if (!string.IsNullOrWhiteSpace(displayName) && !displayName.StartsWith("@"))
                        {
                            data.DisplayName = displayName;
                        }
                    }
                    catch { }

                    // Format fallback display name
                    if (string.IsNullOrEmpty(data.DisplayName))
                    {
                        data.DisplayName = data.IsSystemSound
                            ? "System Sounds"
                            : string.IsNullOrEmpty(data.ProcessName)
                                ? $"Application (PID: {pid})"
                                : FormatProcessName(data.ProcessName);
                    }

                    result.Add(data);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[EZsound] Error reading session {kvp.Key}: {ex.Message}");
                    if (ex is COMException || ex is InvalidComObjectException)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }
            }

            // Remove dead sessions
            foreach (var key in toRemove)
            {
                if (_sessionControls.TryGetValue(key, out var ctrl))
                {
                    if (_sessionEventClients.TryGetValue(key, out var client))
                    {
                        try { ctrl.UnRegisterEventClient(client); } catch { }
                        _sessionEventClients.Remove(key);
                    }
                    _sessionControls.Remove(key);
                }
            }
        }

        return result;
    }

    private static string FormatProcessName(string processName)
    {
        if (string.IsNullOrEmpty(processName)) return processName;

        var formatted = processName.Replace("_", " ").Replace("-", " ");
        if (formatted.Length > 0)
        {
            formatted = char.ToUpper(formatted[0]) + formatted[1..];
        }
        return formatted;
    }

    private static uint GetProcessId(AudioSessionControl session)
    {
        try
        {
            return (uint)session.GetProcessID;
        }
        catch
        {
            // Expected: session may have been closed or detached by the target application.
            return 0;
        }
    }

    private static float GetSimpleVolume(AudioSessionControl session)
    {
        try
        {
            return session.SimpleAudioVolume.Volume;
        }
        catch
        {
            // Expected: session may have terminated before reading volume.
            return 1.0f;
        }
    }

    private static bool GetMuteState(AudioSessionControl session)
    {
        try
        {
            return session.SimpleAudioVolume.Mute;
        }
        catch
        {
            // Expected: session may have terminated before reading mute state.
            return false;
        }
    }

    public void SetSessionVolume(string sessionId, float volume)
    {
        lock (_lock)
        {
            if (_sessionControls.TryGetValue(sessionId, out var session))
            {
                try
                {
                    session.SimpleAudioVolume.Volume = Math.Clamp(volume, 0f, 1f);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[EZsound] Error setting session volume: {ex.Message}");
                }
            }
        }
    }

    public float GetSessionVolume(string sessionId)
    {
        lock (_lock)
        {
            if (_sessionControls.TryGetValue(sessionId, out var session))
            {
                return GetSimpleVolume(session);
            }
        }
        return 1.0f;
    }

    public void SetSessionMute(string sessionId, bool muted)
    {
        lock (_lock)
        {
            if (_sessionControls.TryGetValue(sessionId, out var session))
            {
                try
                {
                    session.SimpleAudioVolume.Mute = muted;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[EZsound] Error setting session mute: {ex.Message}");
                }
            }
        }
    }

    public bool GetSessionMute(string sessionId)
    {
        lock (_lock)
        {
            if (_sessionControls.TryGetValue(sessionId, out var session))
            {
                return GetMuteState(session);
            }
        }
        return false;
    }

    // ─── Master Volume ───────────────────────────────────────────────────

    public float GetMasterVolume()
    {
        lock (_lock)
        {
            try
            {
                if (_defaultDevice?.AudioEndpointVolume != null)
                {
                    return _defaultDevice.AudioEndpointVolume.MasterVolumeLevelScalar;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EZsound] Error getting master volume: {ex.Message}");
            }
        }
        return 1.0f;
    }

    public void SetMasterVolume(float volume)
    {
        lock (_lock)
        {
            try
            {
                if (_defaultDevice?.AudioEndpointVolume != null)
                {
                    _defaultDevice.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(volume, 0f, 1f);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EZsound] Error setting master volume: {ex.Message}");
            }
        }
    }

    public bool GetMasterMute()
    {
        lock (_lock)
        {
            try
            {
                if (_defaultDevice?.AudioEndpointVolume != null)
                {
                    return _defaultDevice.AudioEndpointVolume.Mute;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EZsound] Error getting master mute: {ex.Message}");
            }
        }
        return false;
    }

    public void SetMasterMute(bool muted)
    {
        lock (_lock)
        {
            try
            {
                if (_defaultDevice?.AudioEndpointVolume != null)
                {
                    _defaultDevice.AudioEndpointVolume.Mute = muted;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EZsound] Error setting master mute: {ex.Message}");
            }
        }
    }

    // ─── Real-Time Session Event Notifications ────────────────────────────

    private void SessionManager_OnSessionCreated(object sender, IAudioSessionControl newSession)
    {
        Debug.WriteLine("[EZsound] OnSessionCreated fired by Windows WASAPI!");

        try
        {
            var sessionControl = new AudioSessionControl(newSession);
            var sessionId = GetSessionIdentifier(sessionControl);

            lock (_lock)
            {
                if (!string.IsNullOrEmpty(sessionId) && !_sessionControls.ContainsKey(sessionId))
                {
                    _sessionControls[sessionId] = sessionControl;
                    var client = new SessionEventClient(this, sessionId, sessionControl);
                    _sessionEventClients[sessionId] = client;

                    try
                    {
                        sessionControl.RegisterEventClient(client);
                        Debug.WriteLine($"[EZsound] Immediately hooked event client for new session: {sessionId}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[EZsound] Failed to hook event client on creation: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EZsound] Error wrapping created session: {ex.Message}");
        }

        // Notify UI immediately
        NotifySessionsChanged();

        // Schedule follow-up checks for newly launched processes
        // to ensure executable path and icons are resolved if the process was still initializing
        ScheduleDelayedRefresh(200);
        ScheduleDelayedRefresh(600);
    }

    private void ScheduleDelayedRefresh(int delayMs)
    {
        Task.Delay(delayMs).ContinueWith(_ =>
        {
            if (!_disposed)
            {
                NotifySessionsChanged();
            }
        });
    }

    internal void NotifySessionsChanged()
    {
        SessionsChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void OnSessionVolumeOrMuteChanged(string sessionId, float volume, bool isMuted)
    {
        SessionVolumeChanged?.Invoke(this, (sessionId, volume, isMuted));
    }

    internal void OnSessionExpiredOrDisconnected(string sessionId)
    {
        Debug.WriteLine($"[EZsound] Session expired or disconnected: {sessionId}");

        lock (_lock)
        {
            if (_sessionControls.TryGetValue(sessionId, out var control))
            {
                if (_sessionEventClients.TryGetValue(sessionId, out var client))
                {
                    try 
                    { 
                        control.UnRegisterEventClient(client); 
                    } 
                    catch 
                    { 
                        // Expected: control or client may already be disconnected/invalidated.
                    }
                    _sessionEventClients.Remove(sessionId);
                }
                _sessionControls.Remove(sessionId);
            }
        }

        NotifySessionsChanged();
    }

    // ─── Device Change Handling ──────────────────────────────────────────

    internal void OnDefaultDeviceChanged()
    {
        Debug.WriteLine("[EZsound] Default audio device changed, re-initializing...");
        InitializeDevice();
        NotifySessionsChanged();
        MasterVolumeChanged?.Invoke(this, EventArgs.Empty);
    }

    // ─── Cleanup ─────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            CleanupDevice();

            if (_deviceEnumerator != null)
            {
                try
                {
                    _deviceEnumerator.UnregisterEndpointNotificationCallback(_deviceNotification);
                }
                catch
                {
                    // Expected: callback unregister may throw if COM endpoint is already gone.
                }

                try
                {
                    _deviceEnumerator.Dispose();
                }
                catch
                {
                    // Expected: device enumerator may fail during disposal on runtime shutdown.
                }
                _deviceEnumerator = null;
            }
        }
    }

    // ─── Session Event Client ────────────────────────────────────────────

    private sealed class SessionEventClient : IAudioSessionEventsHandler
    {
        private readonly AudioService _parent;
        private readonly string _sessionId;
        private readonly AudioSessionControl _control;

        public SessionEventClient(AudioService parent, string sessionId, AudioSessionControl control)
        {
            _parent = parent;
            _sessionId = sessionId;
            _control = control;
        }

        public void OnVolumeChanged(float volume, bool isMuted)
        {
            _parent.OnSessionVolumeOrMuteChanged(_sessionId, volume, isMuted);
        }

        public void OnDisplayNameChanged(string displayName)
        {
            _parent.NotifySessionsChanged();
        }

        public void OnIconPathChanged(string iconPath)
        {
            _parent.NotifySessionsChanged();
        }

        public void OnChannelVolumeChanged(uint channelCount, nint newVolumes, uint channelIndex) { }

        public void OnGroupingParamChanged(ref Guid groupingId) { }

        public void OnStateChanged(AudioSessionState state)
        {
            if (state == AudioSessionState.AudioSessionStateExpired)
            {
                _parent.OnSessionExpiredOrDisconnected(_sessionId);
            }
        }

        public void OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason)
        {
            _parent.OnSessionExpiredOrDisconnected(_sessionId);
        }
    }

    // ─── Device Notification Client ──────────────────────────────────────

    private sealed class DeviceNotificationClient : IMMNotificationClient
    {
        private readonly AudioService _service;

        public DeviceNotificationClient(AudioService service)
        {
            _service = service;
        }

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render && role == Role.Multimedia)
            {
                _service.OnDefaultDeviceChanged();
            }
        }

        public void OnDeviceAdded(string pwstrDeviceId) { }

        public void OnDeviceRemoved(string deviceId) { }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState) { }

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }
}
