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
    private readonly List<AudioMetadata> Queue = [];
    private int _currentSongIndex = -1; // -1 = Nothing is playing

    public AudioMetadata? CurrentSong => _currentSongIndex >= 0 && _currentSongIndex < Queue.Count ? Queue[_currentSongIndex] : null;
    public bool IsPlaying { get; private set; }
    public bool IsMediaReady { get; private set; }
    private RepeatMode _repeat = RepeatMode.All;

    public RepeatMode Repeat
    {
        get => _repeat;
        set
        {
            if (_repeat != value)
            {
                _repeat = value;
                RepeatChanged?.Invoke(_repeat);
            }
        }
    }

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

    private bool _internalStop = false;

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
    public event Action<RepeatMode>? RepeatChanged;

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

        if (_internalStop)
        {
            // Internal stop, do NOT auto-play next
            _internalStop = false;
            return;
        }

        // Check if the end of the file has been reached
        if (_audioFileReader != null && _audioFileReader.Position >= _audioFileReader.Length - _audioFileReader.WaveFormat.AverageBytesPerSecond / 10) // Within 100ms of end
        {
            Logger.Info("Media playback ended");
            MediaEnded?.Invoke();

            switch (Repeat)
            {
                case RepeatMode.All:
                    Logger.Debug("Repeat Mode is set to Repeat All");
                    if (_currentSongIndex < Queue.Count - 1)
                    {
                        Logger.Debug("Playing next song in the queue");
                        PlayNext();
                    }
                    else
                    {
                        Logger.Debug("Restarting queue from beginning");
                        _currentSongIndex = 0;
                        PlayQueue(_currentSongIndex);
                    }
                    break;
                case RepeatMode.One:
                    Logger.Debug("Repeat Mode is set to Repeat One");
                    Play();
                    break;
                case RepeatMode.Off:
                    break;
            }
        }
    }

    // Methods
    public void Enqueue(AudioMetadata song)
    {
        if (Queue.Any(s => string.Equals(s.FilePath, song.FilePath, StringComparison.OrdinalIgnoreCase)))
        {
            Logger.Info($"Song already in queue, skipping: {song.Title}");
            return;
        }
        Queue.Add(song);
        Logger.Info($"Enqueued song: {song.Title}");
    }

    public void Enqueue(IEnumerable<AudioMetadata> songs)
    {
        Queue.AddRange(songs);
        Logger.Info($"Enqueued songs: {string.Join(", ", songs.Select(s => s.Title))}");
    }

    public void ClearQueue()
    {
        Queue.Clear();
        _currentSongIndex = -1;
        Stop();
        Logger.Info("Queue cleared");
    }

    public void PlayNext()
    {
        if (Queue.Count == 0)
        {
            Logger.Error("No more songs to play");
            return;
        }

        if (Queue.Count == 1 || Repeat == RepeatMode.One)
        {
            Logger.Info($"Restarting current song");
            _audioFileReader?.Seek(0, SeekOrigin.Begin);
            Play();
            return;
        }


        _currentSongIndex++;
        if (_currentSongIndex >= Queue.Count)
        {
            if (Repeat == RepeatMode.All)
            {
                _currentSongIndex = 0;
                PlayQueue(_currentSongIndex);
            }
            else
            {
                Logger.Error("Reached the end of the queue");
                _currentSongIndex = Queue.Count - 1;
            }
            return;
        }

        AudioMetadata nextSong = Queue[_currentSongIndex];
        Logger.Info($"Playing next song: {nextSong.Title}");
        _ = PlayFileAsync(nextSong.FilePath!);
    }

    public void PlayPrevious()
    {
        if (Queue.Count == 0)
        {
            return;
        }

        if (Queue.Count == 1 || Repeat == RepeatMode.One)
        {
            Logger.Info("Restarting current song");
            _audioFileReader?.Seek(0, SeekOrigin.Begin);
            return;
        }

        _currentSongIndex--;
        if (_currentSongIndex < 0)
        {
            if (Repeat == RepeatMode.All)
            {
                _currentSongIndex = Queue.Count - 1;
                PlayQueue(_currentSongIndex);
            }
            else
            {
                Logger.Info("Already at the first song");
                _currentSongIndex = 0;
            }
            return;
        }

        AudioMetadata prevSong = Queue[_currentSongIndex];
        Logger.Info($"Playing previous song: {prevSong.Title}");
        _ = PlayFileAsync(prevSong.FilePath!);
    }

    public void PlayQueue(int startIndex = 0)
    {
        Logger.Info("Playing queue");
        if (Queue.Count == 0)
        {
            Logger.Error("There are no songs in the queue");
            return;
        }

        Repeat = Queue.Count switch
        {
            1 => RepeatMode.One,
            _ => RepeatMode.All
        };

        _currentSongIndex = Math.Clamp(startIndex, 0, Queue.Count - 1);
        AudioMetadata song = Queue[_currentSongIndex];
        Logger.Info($"Starting queue at: {song.Title}");
        _ = PlayFileAsync(song.FilePath!);
    }

    public async Task PlayFileAsync(string path)
    {
        if (string.IsNullOrEmpty(path) || (!File.Exists(path) && !Uri.IsWellFormedUriString(path, UriKind.Absolute)))
        {
            Logger.Warning($"Media file not found or invalid URL: {path}");
            return;
        }

        // Check if the song is already in the queue
        int indexInQueue = Queue.FindIndex(s => s.FilePath == path);

        if (indexInQueue == -1)
        {
            // If not in the queue, enqueue it
            AudioMetadata metadata = await Task.Run(() => AudioMetadata.Extract(path));
            Queue.Add(metadata);
            _currentSongIndex = Queue.Count - 1; // Set current index to the newly added song
            Logger.Info($"Added and playing new song in queue: {metadata.Title}");
        }
        else
        {
            // Song already in queue, just update the index
            _currentSongIndex = indexInQueue;
            Logger.Info($"Playing existing song in queue: {Queue[_currentSongIndex].Title}");
        }

        _internalStop = true;
        _wavePlayer?.Stop();
        await Task.Delay(50);
        DisposeCurrentMedia();
        IsMediaReady = false;

        try
        {
            _currentFilePath = path;
            _audioFileReader = new AudioFileReader(path);

            // Create volume provider
            _volumeProvider = new VolumeSampleProvider(_audioFileReader)
            {
                Volume = _volume / 100f
            };
            Volume = _volume; // Re-apply volume curve

            _wavePlayer?.Init(_volumeProvider);

            Logger.Info("Audio file loaded successfully");

            if (CurrentSong == null)
            {
                Logger.Error("Song metadata is missing");
                return;
            }

            MetadataLoaded?.Invoke(CurrentSong);

            // Duration notification
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
        if (!IsPlaying)
        {
            return;
        }

        _internalStop = true; // Mark this stop as intentional
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