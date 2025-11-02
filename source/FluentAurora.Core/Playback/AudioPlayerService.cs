using FluentAurora.Core.Indexer;
using FluentAurora.Core.Logging;
using CSCore;
using CSCore.Codecs;
using CSCore.DSP;
using CSCore.SoundOut;
using CSCore.Streams;
using FluentAurora.Core.Settings;

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

    // Settings
    private readonly ISettingsManager? _settingsManager;

    // Queue storage
    private readonly List<AudioMetadata> _queue = new List<AudioMetadata>();
    private List<AudioMetadata>? _originalQueue;
    private readonly HashSet<string> _queuePaths = new HashSet<string>();

    // Playback pipeline
    private ISoundOut? _soundOut;
    private IWaveSource? _waveSource;
    private VolumeSource? _volumeSource;

    // Timer for position updates
    private Timer? _positionTimer;

    // State
    private int _currentSongIndex = -1;
    public bool IsShuffled = false;
    private float _volume = 100f; // 0-100
    private RepeatMode _repeat = RepeatMode.All;
    private bool _isIntentionalStop;
    private bool _isDisposed;

    // Visualizer spectrum
    private SingleBlockNotificationStream? _notificationSource;
    private readonly float[] _fftBuffer = new float[1024];
    private readonly object _fftLock = new object();
    private Timer? _spectrumTimer;
    private FftProvider? _fftProvider;
    private bool _isSpectrumEnabled = true;

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

    public bool IsSpectrumEnabled
    {
        get => _isSpectrumEnabled;
        set
        {
            if (_isSpectrumEnabled == value)
            {
                return;
            }

            _isSpectrumEnabled = value;
            Logger.Info($"Spectrum analyzer {(value ? "enabled" : "disabled")} - Playback state: {(IsPlaying ? "playing" : "not playing")}");

            if (value && IsPlaying)
            {
                StartSpectrumTimer();
            }
            else if (!value)
            {
                StopSpectrumTimer();
            }

            SpectrumEnabledChanged?.Invoke(value);
        }
    }

    // Events
    public event Action? PlaybackStarted;
    public event Action? PlaybackPaused;
    public event Action? PlaybackStopped;
    public event Action<float[]>? SpectrumDataAvailable;
    public event Action<bool>? SpectrumEnabledChanged;
    public event Action<double>? PositionChanged;
    public event Action<double>? DurationChanged;
    public event Action<float>? VolumeChanged;
    public event Action? MediaReady;
    public event Action? MediaEnded;
    public event Action<AudioMetadata>? MetadataLoaded;
    public event Action? MetadataCleared;
    public event Action<RepeatMode>? RepeatModeChanged;
    public event Action? QueueChanged;

    // Constructor
    public AudioPlayerService(ISettingsManager settingsManager)
    {
        Logger.Info("Initializing AudioPlayerService (CSCore)");
        InitializeSoundOut();
        Logger.Info("AudioPlayerService initialized successfully");

        _settingsManager = settingsManager;
        LoadSettings();

        DatabaseManager.SongDeleted += OnSongDeleted;
        DatabaseManager.SongsDeleted += OnSongsDeleted;
    }

    // Methods
    private void LoadSettings()
    {
        if (_settingsManager == null)
        {
            Logger.Warning("Settings Manager not initialized");
            return;
        }

        // Stored as 0-1 (float)
        float savedVolume = _settingsManager.Application.Playback.Volume * 100f;
        _volume = Math.Clamp(savedVolume, 0f, 100f);
        Logger.Info($"Loaded volume from settings: {_volume}%");
    }

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
        Logger.Info("Queue cleared");
        MetadataCleared?.Invoke();
        QueueChanged?.Invoke();
    }

    public void RemoveFromQueue(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        int index = _queue.FindIndex(s => s.FilePath == filePath);
        if (index == -1)
        {
            Logger.Debug($"Song not in queue: {filePath}");
            return;
        }

        RemoveFromQueueByIndex(index);
    }

    public void RemoveFromQueue(List<string> filePaths)
    {
        if (filePaths == null || filePaths.Count == 0)
        {
            return;
        }

        Logger.Info($"Removing {filePaths.Count} songs from queue");
        HashSet<string> pathsToRemove = new HashSet<string>(filePaths.Where(path => !string.IsNullOrEmpty(path)), StringComparer.OrdinalIgnoreCase);

        if (pathsToRemove.Count == 0)
        {
            return;
        }

        // Find all indices to remove
        List<int> indicesToRemove = new List<int>();
        for (int i = 0; i < _queue.Count; i++)
        {
            if (!string.IsNullOrEmpty(_queue[i].FilePath) && pathsToRemove.Contains(_queue[i].FilePath!))
            {
                indicesToRemove.Add(i);
            }
        }

        if (indicesToRemove.Count == 0)
        {
            Logger.Debug($"None of the {filePaths.Count} songs are in queue");
            return;
        }

        Logger.Info($"Found {indicesToRemove.Count} songs to remove from queue");

        // Check if currently playing song will be removed
        bool removingCurrentSong = indicesToRemove.Contains(_currentSongIndex);
        int originalCurrentIndex = _currentSongIndex;

        // Sort in descending order
        indicesToRemove.Sort((a, b) => b.CompareTo(a));

        // Remove from the highest index
        foreach (int index in indicesToRemove)
        {
            AudioMetadata removedSong = _queue[index];
            string? filePath = removedSong.FilePath;

            _queue.RemoveAt(index);
            if (!string.IsNullOrEmpty(filePath))
            {
                _queuePaths.Remove(filePath);
            }

            // Update originalQueue if shuffling was enabled
            if (IsShuffled && _originalQueue != null)
            {
                _originalQueue.Remove(removedSong);
            }

            Logger.Debug($"Removed from queue: {removedSong.Title}");
        }

        // Adjust current song index
        if (removingCurrentSong)
        {
            HandleRemovedCurrentSong(originalCurrentIndex);
        }
        else
        {
            // Calculate how many songs were removed before current song
            int removedBeforeCurrent = indicesToRemove.Count(i => i < originalCurrentIndex);
            _currentSongIndex -= removedBeforeCurrent;
        }

        QueueChanged?.Invoke();
    }

    public void RemoveFromQueueByIndex(int index)
    {
        if (index < 0 || index >= _queue.Count)
        {
            Logger.Warning($"Invalid queue index: {index}");
            return;
        }

        AudioMetadata removedSong = _queue[index];
        string? filePath = removedSong.FilePath;
        _queue.RemoveAt(index);
        if (!string.IsNullOrEmpty(filePath))
        {
            _queuePaths.Remove(filePath);
        }

        // Remove from originalQueue if the queue has been shuffled
        if (IsShuffled && _originalQueue != null)
        {
            _originalQueue.Remove(removedSong);
        }

        Logger.Info($"Removed song from queue: {removedSong.Title}");

        // Handle current song index adjustments
        bool wasPlayingRemovedSong = index == _currentSongIndex;
        bool removedBeforeCurrent = index < _currentSongIndex;

        if (wasPlayingRemovedSong)
        {
            // Trying to play the removed song
            HandleRemovedCurrentSong(index);
        }
        else if (removedBeforeCurrent)
        {
            // Song was before the currently playing one, adjust the index
            _currentSongIndex--;
        }

        QueueChanged?.Invoke();
    }

    private void HandleRemovedCurrentSong(int removedIndex)
    {
        Stop();

        // Queue Empty
        if (_queue.Count == 0)
        {
            _currentSongIndex = -1;
            IsMediaReady = false;
            Logger.Info("Queue is now empty after removing current song");
            MetadataCleared?.Invoke();
            return;
        }

        if (removedIndex < _queue.Count)
        {
            // Try to play next song after removal
            _currentSongIndex = removedIndex;
            Logger.Info($"Playing next song after removal: {_queue[_currentSongIndex].Title}");
            _ = PlayFileAsync(_queue[_currentSongIndex].FilePath!);
        }
        else
        {
            // Try to play last song since the removed one was the last one in the queue
            _currentSongIndex = _queue.Count - 1;
            Logger.Info($"Playing previous song after removal: {_queue[_currentSongIndex].Title}");
            _ = PlayFileAsync(_queue[_currentSongIndex].FilePath!);
        }
    }

    // Public: Playback controls
    public void ToggleShuffle()
    {
        if (_queue.Count == 0)
        {
            return;
        }

        if (!IsShuffled)
        {
            // Shuffling the queue
            _originalQueue = new List<AudioMetadata>(_queue);
            AudioMetadata? currentSong = CurrentSong;

            if (currentSong == null)
            {
                // Shuffle the entire queue since no song is playing
                _queue.Clear();
                _queue.AddRange(_originalQueue.OrderBy(_ => Guid.NewGuid()));
                _currentSongIndex = -1;
                Logger.Info("Shuffled queue (no song selected)");
            }
            else
            {
                // Song is playing, keep it at position 0 and shuffle the rest of the songs in the queue
                List<AudioMetadata> restQueue = _queue.Where(song => song != currentSong).OrderBy(_ => Guid.NewGuid()).ToList();
                _queue.Clear();
                _queue.Add(currentSong);
                _queue.AddRange(restQueue);
                _currentSongIndex = 0;
                Logger.Info($"Shuffled queue with '{currentSong.Title}' as first song");
            }

            IsShuffled = true;
        }
        else
        {
            // Restoring original order of the queue
            if (_originalQueue != null)
            {
                AudioMetadata? currentSong = CurrentSong;
                _queue.Clear();
                _queue.AddRange(_originalQueue);

                if (currentSong != null)
                {
                    // Find the current song in the queue
                    _currentSongIndex = _originalQueue.IndexOf(currentSong);
                    if (_currentSongIndex == -1)
                    {
                        // Song not found??? Rebind to a first song in the queue if this happens (shouldn't though)
                        _currentSongIndex = 0;
                        Logger.Warning($"Could not find '{currentSong.Title}' in original queue");
                    }
                    else
                    {
                        Logger.Info($"Unshuffled queue, current song at index {_currentSongIndex}");
                    }
                }
                else
                {
                    // No song selected
                    _currentSongIndex = -1;
                    Logger.Info("Unshuffled queue (no song selected)");
                }
            }

            IsShuffled = false;
        }

        QueueChanged?.Invoke();
    }

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
            Logger.Info("Deleting the song from the database");
            DatabaseManager.DeleteSong(path);
            throw new FileNotFoundException($"Media file not found: {path}");
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
        StartSpectrumTimer();
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
        StopSpectrumTimer();
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
        StopSpectrumTimer();
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

        if (_settingsManager != null)
        {
            _settingsManager.Application.Playback.Volume = _volume / 100f;
        }
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

        // Create notification stream for spectrum analysis & initialize FFT Provider
        ISampleSource? sampleSource = _waveSource.ToSampleSource();
        _notificationSource = new SingleBlockNotificationStream(sampleSource);
        _notificationSource.SingleBlockRead += OnSingleBlockRead;
        _fftProvider = new FftProvider(sampleSource.WaveFormat.Channels, FftSize.Fft2048);

        _volumeSource = new VolumeSource(_notificationSource);
        SetVolume(_volume);

        _soundOut?.Initialize(_volumeSource.ToWaveSource());
        Logger.Info("Audio file loaded successfully");

        if (CurrentSong == null)
        {
            Logger.Error("Song metadata is missing");
        }
        else
        {
            CurrentSong.ArtworkData ??= DatabaseManager.GetSongArtwork(CurrentSong.FilePath!);
            MetadataLoaded?.Invoke(CurrentSong);
        }

        DurationChanged?.Invoke(GetDuration());
        IsMediaReady = true;
        MediaReady?.Invoke();
        Logger.Info("Media ready");
    }

    private void OnSingleBlockRead(object? sender, SingleBlockReadEventArgs e)
    {
        if (_fftProvider == null)
        {
            return;
        }
        _fftProvider.Add(e.Left, e.Right);
    }


    private void StartSpectrumTimer()
    {
        if (!IsSpectrumEnabled)
        {
            Logger.Debug("StartSpectrumTimer: Spectrum is disabled, skipping");
            return;
        }
        Logger.Info("StartSpectrumTimer: Starting spectrum analysis");
        StopSpectrumTimer();
        _spectrumTimer = new Timer(_ => UpdateSpectrum(), null, 0, 16); // 60 FPS (~16.67ms)
        Logger.Debug("StartSpectrumTimer: Spectrum timer started at 60 FPS");
    }

    private void StopSpectrumTimer()
    {
        _spectrumTimer?.Dispose();
        _spectrumTimer = null;
    }

    private void UpdateSpectrum()
    {
        if (!IsPlaying || _fftProvider == null || !IsSpectrumEnabled)
        {
            return;
        }

        try
        {
            // Check if the provider has enough data and is ready
            if (!_fftProvider.IsNewDataAvailable)
            {
                return;
            }

            // fftData should hold at least 2048 elements
            float[] fftData = new float[2048];

            // Safety check to properly check if the _fftProvider has FftSize of 2048
            if (_fftProvider.FftSize != FftSize.Fft2048)
            {
                Logger.Warning($"FFT size mismatch. Expected Fft2048, got {_fftProvider.FftSize}");
                return;
            }

            // Try to get FFT data (handle the case where there isn't enough data
            bool success;
            try
            {
                success = _fftProvider.GetFftData(fftData);
            }
            catch (ArgumentException)
            {
                // Not enough data
                return;
            }

            if (!success)
            {
                // Silently return since this is normal behaviour
                return;
            }

            // Using positive frequencies from FFT Data
            float[] processedData = new float[64];

            int usableDataLength = fftData.Length / 2;
            int bandsPerGroup = usableDataLength / processedData.Length;

            for (int i = 0; i < processedData.Length; i++)
            {
                float sum = 0;
                int count = 0;

                for (int j = 0; j < bandsPerGroup; j++)
                {
                    int index = i * bandsPerGroup + j;
                    if (index >= usableDataLength)
                    {
                        continue;
                    }

                    // Squaring the value for a more dramatic response
                    float value = fftData[index];
                    value = value * value;

                    // Frequency-based boost favouring mid to high frequencies
                    float frequencyBoost = 1.0f + (i / (float)processedData.Length) * 4.0f;
                    value *= frequencyBoost;

                    sum += value;
                    count++;
                }

                // Power scaling for more sensitivity
                // Significantly amplified
                // Logarithmic scaling for better visualization
                float average = count > 0 ? sum / count : 0;
                average = (float)Math.Pow(average, 0.6);
                average *= 50.0f;
                average = (float)Math.Log10(1 + average * 9);

                processedData[i] = Math.Min(1.0f, average);
            }

            SpectrumDataAvailable?.Invoke(processedData);
        }
        catch (ArgumentException)
        {
            Logger.Debug("FFT data not available during transition");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error updating spectrum: {ex}");
        }
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

    private void OnSongDeleted(string filePath)
    {
        Logger.Info($"Received notification that song was deleted from database: {filePath}");
        RemoveFromQueue(filePath);
    }

    private void OnSongsDeleted(List<string> files)
    {
        Logger.Info($"Received notification that songs were deleted from the database: {files.Count}");
        RemoveFromQueue(files);
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
            StopSpectrumTimer();
            if (_notificationSource != null)
            {
                _notificationSource.SingleBlockRead -= OnSingleBlockRead;
                _notificationSource.Dispose();
                _notificationSource = null;
            }
            _fftProvider = null;

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
            if (_settingsManager != null)
            {
                _settingsManager.Application.Playback.Volume = _volume / 100f;
                _settingsManager.SaveAll();
                Logger.Debug("Saved volume to settings");
            }

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
        DatabaseManager.SongDeleted -= OnSongDeleted;
        _isDisposed = true;
        Logger.Info("AudioPlayerService disposed");
    }
}