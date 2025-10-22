using FluentAurora.Core.Indexer;
using FluentAurora.Core.Logging;
using CSCore;
using CSCore.Codecs;
using CSCore.SoundOut;
using CSCore.Streams;

namespace FluentAurora.Core.Playback;

public sealed class AudioPlayerService : IDisposable
{
    // Constants
    private const int POSITION_UPDATE_INTERVAL_MS = 100;
    private const int DESIRED_LATENCY_MS = 100;
    private const float VOLUME_SOFT_CURVE_THRESHOLD = 0.5f;
    private const float VOLUME_SOFT_CURVE_FACTOR = 0.6f;
    private const float VOLUME_FAST_CURVE_BASE = 0.3f;
    private const float VOLUME_FAST_CURVE_FACTOR = 1.4f;

    // Queue storage
    private readonly List<AudioMetadata> _queue = new List<AudioMetadata>();
    private readonly HashSet<string> _queuePaths = new HashSet<string>();

    // Playback pipeline
    private ISoundOut? _soundOut;
    private IWaveSource? _waveSource;
    private VolumeSource? _volumeSource;

    // Timer for position updates
    private Timer? _positionTimer;

    // State
    private int _currentSongIndex = -1;
    private float _volume = 100f; // 0-100
    private RepeatMode _repeat = RepeatMode.All;
    private bool _isIntentionalStop;
    private bool _isDisposed;

    // Public properties
    public AudioMetadata? CurrentSong => _currentSongIndex >= 0 && _currentSongIndex < _queue.Count ? _queue[_currentSongIndex] : null;

    public bool IsPlaying { get; private set; }
    public bool IsMediaReady { get; private set; }

    public List<AudioMetadata> Queue => _queue;
    public int CurrentIndex => _currentSongIndex;

    public RepeatMode Repeat
    {
        get => _repeat;
        set
        {
            if (_repeat == value)
            {
                return;
            }
            _repeat = value;
            RepeatModeChanged?.Invoke(_repeat);
        }
    }

    public float Volume
    {
        get => _volume;
        set => SetVolume(value);
    }

    // Events
    public event Action? PlaybackStarted;
    public event Action? PlaybackPaused;
    public event Action? PlaybackStopped;
    public event Action<double>? PositionChanged;
    public event Action<double>? DurationChanged;
    public event Action<float>? VolumeChanged;
    public event Action? MediaReady;
    public event Action? MediaEnded;
    public event Action<AudioMetadata>? MetadataLoaded;
    public event Action<RepeatMode>? RepeatModeChanged;
    public event Action? QueueChanged;

    // Constructor
    public AudioPlayerService()
    {
        Logger.Info("Initializing AudioPlayerService (CSCore)");
        InitializeSoundOut();
        Logger.Info("AudioPlayerService initialized successfully");
    }

    // Methods
    // Public: Queue management
    public void Enqueue(AudioMetadata song)
    {
        ArgumentNullException.ThrowIfNull(song);

        if (string.IsNullOrEmpty(song.FilePath))
        {
            Logger.Warning("Song has no file path, cannot enqueue");
            return;
        }

        if (!_queuePaths.Add(song.FilePath))
        {
            Logger.Info($"Song already in queue, skipping: {song.Title}");
            return;
        }

        _queue.Add(song);
        Logger.Info($"Enqueued song: {song.Title}");
        QueueChanged?.Invoke();
    }

    public void Enqueue(IEnumerable<AudioMetadata> songs)
    {
        List<AudioMetadata> newSongs = songs.Where(s => !string.IsNullOrEmpty(s.FilePath) && _queuePaths.Add(s.FilePath)).ToList();
        if (newSongs.Count == 0)
        {
            return;
        }

        _queue.AddRange(newSongs);
        Logger.Info($"Enqueued {newSongs.Count} songs");
        QueueChanged?.Invoke();
    }

    public void ClearQueue()
    {
        _queue.Clear();
        _queuePaths.Clear();
        _currentSongIndex = -1;
        Stop();
        Logger.Info("Queue cleared");
        QueueChanged?.Invoke();
    }

    // Public: Playback controls
    public void PlayQueue(int startIndex = 0)
    {
        if (_queue.Count == 0)
        {
            Logger.Error("Queue is empty");
            return;
        }

        Repeat = _queue.Count == 1 ? RepeatMode.One : RepeatMode.All;
        _currentSongIndex = Math.Clamp(startIndex, 0, _queue.Count - 1);

        AudioMetadata song = _queue[_currentSongIndex];
        Logger.Info($"Starting queue at: {song.Title}");
        _ = PlayFileAsync(song.FilePath!);
    }

    public async Task PlayFileAsync(string path)
    {
        if (!IsValidPath(path))
        {
            Logger.Warning($"Invalid media path: {path}");
            return;
        }

        await PrepareForNewFile(path).ConfigureAwait(false);

        try
        {
            LoadAudioFile(path);
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
        if (!CanPlay())
        {
            Logger.Warning("Cannot play - media not ready");
            return;
        }

        Logger.Info("Starting playback");
        ResetPositionIfAtEnd();

        _soundOut!.Play();
        IsPlaying = true;
        PlaybackStarted?.Invoke();
        StartPositionTimer();
    }

    public void Pause()
    {
        if (_soundOut?.PlaybackState != PlaybackState.Playing)
        {
            Logger.Warning("Cannot pause - not currently playing");
            return;
        }

        Logger.Info("Pausing playback");
        _soundOut.Pause();
        IsPlaying = false;
        PlaybackPaused?.Invoke();
        StopPositionTimer();
    }

    public void Stop()
    {
        if (!IsPlaying)
        {
            return;
        }

        _isIntentionalStop = true;
        Logger.Info("Stopping playback");
        StopPositionTimer();
        _soundOut?.Stop();
        ResetPosition();
        IsPlaying = false;
        Logger.Debug("Playback stopped and position reset");
    }

    public void PlayNext()
    {
        if (!CanNavigate())
        {
            return;
        }

        if (ShouldRestartCurrent())
        {
            RestartCurrent();
            return;
        }

        NavigateToIndex(GetNextIndex());
    }

    public void PlayPrevious()
    {
        if (!CanNavigate())
        {
            return;
        }

        if (ShouldRestartCurrent())
        {
            RestartCurrent();
            return;
        }

        NavigateToIndex(GetPreviousIndex());
    }

    public void SeekTo(double positionMs)
    {
        if (!IsMediaReady || _waveSource == null)
        {
            Logger.Warning("Cannot seek - media not ready");
            return;
        }

        Logger.Info($"Seeking to position: {positionMs}ms");

        TimeSpan target = ClampPosition(TimeSpan.FromMilliseconds(positionMs));
        bool wasPlaying = IsPlaying;

        StopPositionTimer();
        _waveSource.SetPosition(target);
        PositionChanged?.Invoke(target.TotalMilliseconds);

        if (wasPlaying)
        {
            StartPositionTimer();
        }
    }

    public double GetCurrentPosition() => _waveSource?.GetPosition().TotalMilliseconds ?? 0;

    public double GetDuration() => _waveSource?.GetLength().TotalMilliseconds ?? 0;

    // Private: Initialization
    private void InitializeSoundOut()
    {
        try
        {
            _soundOut = new WasapiOut() { Latency = DESIRED_LATENCY_MS };
            _soundOut.Stopped += OnPlaybackStopped;
            Logger.Debug("SoundOut initialized");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to initialize SoundOut: {ex}");
            throw;
        }
    }

    // Private: Volume Control
    private void SetVolume(float value)
    {
        _volume = Math.Clamp(value, 0.0f, 100.0f);
        float adjustedVolume = CalculateAdjustedVolume(_volume);

        if (_volumeSource != null)
        {
            _volumeSource.Volume = adjustedVolume;
        }

        Logger.Info($"Volume set to {_volume}% (Adjusted: {adjustedVolume * 100}%)");
        VolumeChanged?.Invoke(_volume);
    }

    private static float CalculateAdjustedVolume(float volume)
    {
        float normalized = volume / 100.0f;
        return volume <= 50.0f ? normalized * VOLUME_SOFT_CURVE_FACTOR : VOLUME_FAST_CURVE_BASE + (normalized - VOLUME_SOFT_CURVE_THRESHOLD) * VOLUME_FAST_CURVE_FACTOR;
    }

    // Private: Validation and small helper
    private bool IsValidPath(string path) => !string.IsNullOrEmpty(path) && (File.Exists(path) || Uri.IsWellFormedUriString(path, UriKind.Absolute));

    private bool CanPlay() => IsMediaReady && _soundOut != null && _waveSource != null;

    private bool CanNavigate() => _queue.Count > 0;

    private bool ShouldRestartCurrent() => _queue.Count == 1 || Repeat == RepeatMode.One;

    private void RestartCurrent()
    {
        Logger.Info("Restarting current song");
        _waveSource?.SetPosition(TimeSpan.Zero);
        Play();
    }

    private void ResetPositionIfAtEnd()
    {
        if (_waveSource != null && _waveSource.Position >= _waveSource.Length)
        {
            _waveSource.Position = 0;
        }
    }

    private void ResetPosition()
    {
        if (_waveSource != null)
        {
            _waveSource.Position = 0;
        }
    }

    private TimeSpan ClampPosition(TimeSpan position)
    {
        if (_waveSource == null)
        {
            return TimeSpan.Zero;
        }

        TimeSpan total = _waveSource.GetLength();
        if (position > total)
        {
            return total;
        }
        if (position < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }
        return position;
    }

    private int GetNextIndex()
    {
        int next = _currentSongIndex + 1;
        if (next >= _queue.Count)
        {
            return Repeat == RepeatMode.All ? 0 : _currentSongIndex;
        }
        return next;
    }

    private int GetPreviousIndex()
    {
        int prev = _currentSongIndex - 1;
        if (prev < 0)
        {
            return Repeat == RepeatMode.All ? _queue.Count - 1 : 0;
        }
        return prev;
    }

    private void NavigateToIndex(int index)
    {
        if (index == _currentSongIndex)
        {
            return;
        }

        _currentSongIndex = index;
        AudioMetadata song = _queue[_currentSongIndex];
        Logger.Info($"Playing song: {song.Title}");
        _ = PlayFileAsync(song.FilePath!);
    }

    // Private: Song Loading/Preparation
    private async Task PrepareForNewFile(string path)
    {
        int indexInQueue = _queue.FindIndex(s => s.FilePath == path);

        if (indexInQueue == -1)
        {
            AudioMetadata? metadata = await Task.Run(() => DatabaseManager.GetSongByFilePath(path)).ConfigureAwait(false);
            if (metadata == null)
            {
                Logger.Error("Failed to find specific song in DB, adding it to the index");
                DatabaseManager.AddSong(path);
                metadata = await Task.Run(() => DatabaseManager.GetSongByFilePath(path)).ConfigureAwait(false);
            }
            
            // If there was an error while adding the song to the database, create a temporary metadata
            metadata ??= new AudioMetadata { FilePath = path, Title = Path.GetFileNameWithoutExtension(path) };

            _queue.Add(metadata);
            if (!string.IsNullOrEmpty(metadata.FilePath)) _queuePaths.Add(metadata.FilePath);
            _currentSongIndex = _queue.Count - 1;
            Logger.Info($"Added new song to queue: {metadata.Title}");
            QueueChanged?.Invoke();
        }
        else
        {
            _currentSongIndex = indexInQueue;
            Logger.Info($"Playing existing song: {_queue[_currentSongIndex].Title}");
        }

        _isIntentionalStop = true;
        _soundOut?.Stop();
        await Task.Delay(50).ConfigureAwait(false);
        _isIntentionalStop = false; // Reset the flag after stopping (in case RepeatMode is set to All/One and there's 1 track)
        DisposeCurrentMedia();
        IsMediaReady = false;
    }

    private void LoadAudioFile(string path)
    {
        _waveSource = CodecFactory.Instance.GetCodec(path);

        _volumeSource = new VolumeSource(_waveSource.ToSampleSource());
        SetVolume(_volume);

        _soundOut?.Initialize(_volumeSource.ToWaveSource());
        Logger.Info("Audio file loaded successfully");

        if (CurrentSong == null)
        {
            Logger.Error("Song metadata is missing");
            // Can play, but metadata events won't trigger
        }
        else
        {
            // Try to fill artwork from DB if missing
            CurrentSong.ArtworkData ??= DatabaseManager.GetSongArtwork(CurrentSong.FilePath!);
            MetadataLoaded?.Invoke(CurrentSong);
        }

        DurationChanged?.Invoke(GetDuration());
        IsMediaReady = true;
        MediaReady?.Invoke();
        Logger.Info("Media ready");
    }

    // Playback stopped handler
    private void OnPlaybackStopped(object? sender, PlaybackStoppedEventArgs e)
    {
        if (e.HasError)
        {
            Logger.Error($"Playback stopped with error: {e.Exception}");
            return;
        }

        Logger.Info("Playback stopped");
        IsPlaying = false;
        PlaybackStopped?.Invoke();

        if (_isIntentionalStop)
        {
            _isIntentionalStop = false;
            return;
        }

        if (IsEndOfFile())
        {
            HandleMediaEnded();
        }
    }

    private bool IsEndOfFile()
    {
        if (_waveSource == null)
        {
            return false;
        }

        // allow small tolerance
        int toleranceBytes = _waveSource.WaveFormat.BytesPerSecond / 10;
        return _waveSource.Position >= _waveSource.Length - toleranceBytes;
    }

    private void HandleMediaEnded()
    {
        Logger.Info("Media playback ended");
        MediaEnded?.Invoke();

        switch (Repeat)
        {
            case RepeatMode.All:
                HandleRepeatAll();
                break;
            case RepeatMode.One:
                RestartCurrent();
                break;
            case RepeatMode.Off:
            default:
                // nothing
                break;
        }
    }

    private void HandleRepeatAll()
    {
        if (_currentSongIndex < _queue.Count - 1)
        {
            PlayNext();
        }
        else
        {
            _currentSongIndex = 0;
            PlayQueue();
        }
    }

    // Position timer
    private void StartPositionTimer()
    {
        StopPositionTimer();
        _positionTimer = new Timer(_ => UpdatePosition(), null, 0, POSITION_UPDATE_INTERVAL_MS);
    }

    private void UpdatePosition()
    {
        if (IsPlaying && _waveSource != null)
        {
            PositionChanged?.Invoke(GetCurrentPosition());
        }
    }

    private void StopPositionTimer()
    {
        _positionTimer?.Dispose();
        _positionTimer = null;
    }

    // Disposal of current media resources
    private void DisposeCurrentMedia()
    {
        try
        {
            _volumeSource?.Dispose();
            _volumeSource = null;

            _waveSource?.Dispose();
            _waveSource = null;

            Logger.Debug("Current media disposed");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error disposing current media: {ex}");
        }
    }

    // Dispose
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
            _soundOut?.Stop();
            _soundOut?.Dispose();
            _soundOut = null;
            Logger.Debug("SoundOut disposed");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error disposing SoundOut: {ex}");
        }

        DisposeCurrentMedia();
        _isDisposed = true;
        Logger.Info("AudioPlayerService disposed");
    }
}