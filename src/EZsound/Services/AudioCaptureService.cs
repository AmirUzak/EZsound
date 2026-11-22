using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;

namespace EZsound.Services;

/// <summary>
/// Implements master audio capture using NAudio WASAPI Loopback Capture.
/// Computes real-time RMS and peak energy and applies smooth attack/decay for visualizers.
/// </summary>
public sealed class AudioCaptureService : IAudioCaptureService
{
    private readonly object _lock = new();
    private WasapiLoopbackCapture? _capture;
    private MMDeviceEnumerator? _deviceEnumerator;
    private readonly DeviceNotificationClient _notificationClient;
    private readonly System.Threading.Timer _smoothingTimer;
    private bool _disposed;
    private bool _isRunning;

    private float _currentLevel;
    private float _targetLevel;
    private long _lastDataTimestamp;

    public event EventHandler<float>? LevelUpdated;

    public float CurrentLevel
    {
        get
        {
            lock (_lock)
            {
                return _currentLevel;
            }
        }
    }

    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return _isRunning;
            }
        }
    }

    public AudioCaptureService()
    {
        _notificationClient = new DeviceNotificationClient(this);

        try
        {
            _deviceEnumerator = new MMDeviceEnumerator();
            _deviceEnumerator.RegisterEndpointNotificationCallback(_notificationClient);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EZsound] Failed to register device notification in AudioCaptureService: {ex.Message}");
        }

        // 60 FPS smoothing and decay timer (~16ms period)
        _smoothingTimer = new System.Threading.Timer(OnSmoothingTick, null, 16, 16);
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_isRunning) return;
            _isRunning = true;
            InitializeCaptureInternal();
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning) return;
            _isRunning = false;
            CleanupCaptureInternal();
            _currentLevel = 0f;
            _targetLevel = 0f;
        }

        LevelUpdated?.Invoke(this, 0f);
    }

    public void Restart()
    {
        lock (_lock)
        {
            if (!_isRunning) return;
            CleanupCaptureInternal();
            InitializeCaptureInternal();
        }
    }

    private void InitializeCaptureInternal()
    {
        try
        {
            var device = WasapiLoopbackCapture.GetDefaultLoopbackCaptureDevice();
            if (device == null)
            {
                Debug.WriteLine("[EZsound] No default loopback capture device found.");
                return;
            }

            _capture = new WasapiLoopbackCapture(device);
            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;
            _capture.StartRecording();
            Debug.WriteLine($"[EZsound] AudioCaptureService started on '{device.FriendlyName}'");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EZsound] Error starting WasapiLoopbackCapture: {ex.Message}");
            _capture = null;
        }
    }

    private void CleanupCaptureInternal()
    {
        if (_capture != null)
        {
            try
            {
                _capture.DataAvailable -= OnDataAvailable;
                _capture.RecordingStopped -= OnRecordingStopped;
                _capture.StopRecording();
            }
            catch (Exception ex)
            {
                // Expected: WASAPI capture device may already be stopped or invalidated (e.g. unplugged).
                App.Log($"[AudioCaptureService] StopRecording notice: {ex.Message}");
            }

            try
            {
                _capture.Dispose();
            }
            catch (Exception ex)
            {
                // Expected: COM resources may throw during disposal if audio endpoint became invalid.
                App.Log($"[AudioCaptureService] Capture dispose notice: {ex.Message}");
            }

            _capture = null;
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0 || _capture == null) return;

        float sumSquares = 0f;
        int sampleCount = 0;
        float peak = 0f;

        try
        {
            var format = _capture.WaveFormat;
            if (format.Encoding == WaveFormatEncoding.IeeeFloat)
            {
                int totalSamples = e.BytesRecorded / 4;
                // Subsample every 2 samples for high efficiency
                for (int i = 0; i < totalSamples; i += 2)
                {
                    float sample = BitConverter.ToSingle(e.Buffer, i * 4);
                    float abs = Math.Abs(sample);
                    if (abs > peak) peak = abs;
                    sumSquares += sample * sample;
                    sampleCount++;
                }
            }
            else if (format.BitsPerSample == 16)
            {
                int totalSamples = e.BytesRecorded / 2;
                for (int i = 0; i < totalSamples; i += 2)
                {
                    short sample = BitConverter.ToInt16(e.Buffer, i * 2);
                    float normalized = sample / 32768.0f;
                    float abs = Math.Abs(normalized);
                    if (abs > peak) peak = abs;
                    sumSquares += normalized * normalized;
                    sampleCount++;
                }
            }

            if (sampleCount > 0)
            {
                float rms = (float)Math.Sqrt(sumSquares / sampleCount);

                // Combine RMS power with peak transient for vivid visual response
                // Typically RMS is between 0.05 and 0.40, so we scale by 2.6
                float rawLevel = Math.Clamp((rms * 2.6f) + (peak * 0.35f), 0f, 1f);

                lock (_lock)
                {
                    _targetLevel = rawLevel;
                    _lastDataTimestamp = Stopwatch.GetTimestamp();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EZsound] Error processing audio buffer in capture: {ex.Message}");
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            Debug.WriteLine($"[EZsound] Loopback recording stopped with exception: {e.Exception.Message}");
        }

        lock (_lock)
        {
            _targetLevel = 0f;
        }
    }

    private void OnSmoothingTick(object? state)
    {
        if (_disposed) return;

        float levelToReport;

        lock (_lock)
        {
            if (!_isRunning)
            {
                levelToReport = 0f;
                _currentLevel = 0f;
            }
            else
            {
                // In WASAPI loopback, if no app plays sound, DataAvailable stops firing.
                // Detect silence when no packets arrived for > 90ms.
                long elapsedTicks = Stopwatch.GetTimestamp() - _lastDataTimestamp;
                double elapsedMs = (double)elapsedTicks / Stopwatch.Frequency * 1000.0;

                if (elapsedMs > 90.0)
                {
                    _targetLevel = 0f;
                }

                if (_targetLevel > _currentLevel)
                {
                    // Fast attack: react promptly to beats and volume peaks
                    _currentLevel += (_targetLevel - _currentLevel) * 0.45f;
                }
                else
                {
                    // Smooth decay: organic falloff without abrupt drop
                    _currentLevel *= 0.88f;
                    if (_currentLevel < 0.003f)
                    {
                        _currentLevel = 0f;
                    }
                }

                levelToReport = _currentLevel;
            }
        }

        LevelUpdated?.Invoke(this, levelToReport);
    }

    internal void OnDefaultDeviceChanged()
    {
        Debug.WriteLine("[EZsound] Default audio render device changed; restarting visualizer capture...");
        Task.Delay(100).ContinueWith(_ =>
        {
            if (!_disposed)
            {
                Restart();
            }
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _smoothingTimer.Dispose();

        lock (_lock)
        {
            CleanupCaptureInternal();

            if (_deviceEnumerator != null)
            {
                try
                {
                    _deviceEnumerator.UnregisterEndpointNotificationCallback(_notificationClient);
                    _deviceEnumerator.Dispose();
                }
                catch (Exception ex)
                {
                    // Expected: COM endpoint enumerator may already be released or invalidated on shutdown.
                    App.Log($"[AudioCaptureService] Enumerator cleanup notice: {ex.Message}");
                }
                _deviceEnumerator = null;
            }
        }
    }

    private sealed class DeviceNotificationClient : IMMNotificationClient
    {
        private readonly AudioCaptureService _parent;

        public DeviceNotificationClient(AudioCaptureService parent)
        {
            _parent = parent;
        }

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render && role == Role.Multimedia)
            {
                _parent.OnDefaultDeviceChanged();
            }
        }

        public void OnDeviceAdded(string pwstrDeviceId) { }
        public void OnDeviceRemoved(string deviceId) { }
        public void OnDeviceStateChanged(string deviceId, DeviceState newState) { }
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }
}
