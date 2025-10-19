using FluentAurora.Core.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace FluentAurora.Core.Playback;

public class AudioPlayerService : IDisposable
{
    // Properties
    private IWavePlayer? _wavePlayer;
    private AudioFileReader? _audioFileReader;
    private VolumeSampleProvider? _volumeProvider;
    private string? _currentFilePath;
    private Timer? _positionTimer;
    private bool _isDisposed;

    public AudioMetadata? CurrentMetadata { get; private set; }
    public bool IsPlaying { get; private set; }
    public bool IsMediaReady { get; private set; }
    public bool IsLooping { get; set; } = true;

    private int _volume = 100;

    public int Volume
    {
        get => _volume;
        set
        {
            // Volume Curve
            _volume = Math.Clamp(value, 0, 100);
            float normalized = _volume / 100f;
            float adjusted = _volume switch
            {
                <= 50 => normalized * 0.6f, // Softer curve below 50% volume
                _ => 0.3f + (normalized - 0.5f) * 1.4f // Faster curve after 50% volume than linear curve
            };

            if (_volumeProvider != null)
            {
                _volumeProvider.Volume = adjusted;
            }

            Logger.Info($"Setting volume to {_volume}% (Internal: {(adjusted * 100)}%)");
            
            VolumeChanged?.Invoke(_volume);
        }
    }

    // Events
    public event Action? PlaybackStarted;
    public event Action? PlaybackPaused;
    public event Action? PlaybackStopped;
    public event Action<int>? PositionChanged;
    public event Action<int>? DurationChanged;
    public event Action<int>? VolumeChanged;
    public event Action? MediaReady;
    public event Action? MediaEnded;
    public event Action<AudioMetadata>? MetadataLoaded;

    // Constructor
    public AudioPlayerService()
    {
        Logger.Info("Initializing AudioPlayerService (NAudio)");
        InitializeWavePlayer();
        Logger.Info("AudioPlayerService initialized successfully");
    }

    private void InitializeWavePlayer()
    {
        try
        {
            _wavePlayer = new WaveOutEvent
            {
                DesiredLatency = 100
            };

            _wavePlayer.PlaybackStopped += OnPlaybackStopped;
            Logger.Debug("WavePlayer initialized");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to initialize WavePlayer: {ex}");
            throw;
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            Logger.Error($"Playback stopped with error: {e.Exception}");
            return;
        }

        Logger.Info("Playback stopped");
        IsPlaying = false;
        PlaybackStopped?.Invoke();

        // Check if the end of the file has been reached
        if (_audioFileReader != null && _audioFileReader.Position >= _audioFileReader.Length - _audioFileReader.WaveFormat.AverageBytesPerSecond / 10) // Within 100ms of end
        {
            Logger.Info("Media playback ended");
            MediaEnded?.Invoke();

            if (IsLooping && IsMediaReady)
            {
                Logger.Debug("Looping enabled, restarting playback");
                Task.Run(async () =>
                {
                    await Task.Delay(50);
                    Play();
                });
            }
        }
    }

    // Methods
    public async Task PlayFileAsync(string path)
    {
        Logger.Info($"PlayFileAsync called with path: {path}");

        if (!System.IO.File.Exists(path) && !Uri.IsWellFormedUriString(path, UriKind.Absolute))
        {
            Logger.Warning($"Media file not found or invalid URL: {path}");
            return;
        }

        Stop();
        DisposeCurrentMedia();
        IsMediaReady = false;

        try
        {
            _currentFilePath = path;
            Logger.Debug($"Loading local file: {path}");
            _audioFileReader = new AudioFileReader(path);

            // Create volume provider
            _volumeProvider = new VolumeSampleProvider(_audioFileReader)
            {
                Volume = _volume / 100f
            };
            Volume = _volume; // Re-apply volume with curve

            // Initialize the wave player with the audio
            _wavePlayer?.Init(_volumeProvider);

            Logger.Info("Audio file loaded successfully");

            // Extract metadata from the file
            CurrentMetadata = await Task.Run(() => AudioMetadata.Extract(path));
            MetadataLoaded?.Invoke(CurrentMetadata);

            // Duration notification (To update UI)
            int durationMs = (int)(_audioFileReader.TotalTime.TotalMilliseconds);
            DurationChanged?.Invoke(durationMs);

            IsMediaReady = true;
            MediaReady?.Invoke();
            Logger.Info("Media is ready (Invoking MediaReady event)");

            Play();
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load media file '{path}': {ex}");
            IsMediaReady = false;
        }
    }

    public void Play()
    {
        if (!IsMediaReady || _wavePlayer == null || _audioFileReader == null)
        {
            Logger.Warning("Play() called but media is not ready");
            return;
        }

        Logger.Info("Starting playback");

        // If at the end, restart from beginning
        if (_audioFileReader.Position >= _audioFileReader.Length)
        {
            _audioFileReader.Position = 0;
        }

        _wavePlayer.Play();
        IsPlaying = true;
        PlaybackStarted?.Invoke();

        // Start position update timer
        StartPositionTimer();
    }

    public void Pause()
    {
        if (_wavePlayer?.PlaybackState != PlaybackState.Playing)
        {
            Logger.Warning("Pause() called but not playing");
            return;
        }

        Logger.Info("Pausing playback");
        _wavePlayer?.Pause();
        IsPlaying = false;
        PlaybackPaused?.Invoke();

        StopPositionTimer();
    }

    public void Stop()
    {
        Logger.Info("Stopping playback");

        StopPositionTimer();
        _wavePlayer?.Stop();

        if (_audioFileReader != null)
        {
            _audioFileReader.Position = 0;
        }

        IsPlaying = false;
        Logger.Debug("Playback stopped and position reset");
    }

    public void SeekTo(int positionMs)
    {
        if (!IsMediaReady || _audioFileReader == null)
        {
            Logger.Warning("SeekTo() called but media is not ready");
            return;
        }

        Logger.Info($"Seeking to position: {positionMs}ms");

        TimeSpan targetPosition = TimeSpan.FromMilliseconds(positionMs);
        if (targetPosition > _audioFileReader.TotalTime)
        {
            targetPosition = _audioFileReader.TotalTime;
        }
        else if (targetPosition < TimeSpan.Zero)
        {
            targetPosition = TimeSpan.Zero;
        }

        // Stop position timer to avoid conflicts
        bool wasPlaying = IsPlaying;
        StopPositionTimer();

        _audioFileReader.CurrentTime = targetPosition;
        PositionChanged?.Invoke(positionMs);

        // Resume position timer if the song is playing
        if (wasPlaying)
        {
            StartPositionTimer();
        }
    }

    public int GetCurrentPosition()
    {
        return (int)(_audioFileReader?.CurrentTime.TotalMilliseconds ?? 0);
    }

    public int GetDuration()
    {
        return (int)(_audioFileReader?.TotalTime.TotalMilliseconds ?? 0);
    }

    private void StartPositionTimer()
    {
        StopPositionTimer();
        _positionTimer = new Timer(_ =>
        {
            if (IsPlaying && _audioFileReader != null)
            {
                PositionChanged?.Invoke(GetCurrentPosition());
            }
        }, null, 0, 100); // Update every 100ms
    }

    private void StopPositionTimer()
    {
        _positionTimer?.Dispose();
        _positionTimer = null;
    }

    private void DisposeCurrentMedia()
    {
        try
        {
            _audioFileReader?.Dispose();
            _audioFileReader = null;
            _volumeProvider = null;
            Logger.Debug("Current media disposed");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error disposing current media: {ex}");
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Logger.Info("Disposing AudioPlayerService");

        try
        {
            StopPositionTimer();
            _wavePlayer?.Stop();
            _wavePlayer?.Dispose();
            Logger.Debug("WavePlayer disposed");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error disposing WavePlayer: {ex}");
        }

        DisposeCurrentMedia();

        _isDisposed = true;
        Logger.Info("AudioPlayerService disposed");
    }
}