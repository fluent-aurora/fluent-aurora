using Avalonia.Media.Imaging;
using LibVLCSharp.Shared;
using FluentAurora.Core.Logging;
using TagLib;
using File = System.IO.File;

namespace FluentAurora.Core.Playback;

public class AudioPlayerService : IDisposable
{
    // Properties
    private readonly LibVLC _libVLC;
    private readonly MediaPlayer _mediaPlayer;
    private Media? _currentMedia;
    public AudioMetadata? CurrentMetadata { get; private set; }
    public bool IsPlaying => _mediaPlayer.IsPlaying;
    public bool IsMediaReady { get; private set; }
    public bool IsLooping { get; set; } = true;

    public int Volume
    {
        get => _mediaPlayer.Volume;
        set
        {
            // Below 50%, compress way more
            // Above 50%, expand faster
            int adjusted = value switch
            {
                <= 50 => (int)(value * 0.6),
                _ => (int)(30 + (value - 50) * 1.4)
            };
            _mediaPlayer.Volume = adjusted;
            Logger.Trace($"Setting volume to {value}% (LibVLC internal: {adjusted}%)");
        }
    }

    // Events
    public event Action? PlaybackStarted;
    public event Action? PlaybackPaused;
    public event Action? PlaybackStopped;
    public event Action<int>? PositionChanged;
    public event Action<int>? DurationChanged;
    public event Action? MediaReady;
    public event Action? MediaEnded;
    public event Action<AudioMetadata>? MetadataLoaded;

    // Constructor
    public AudioPlayerService()
    {
        Logger.Info("Initializing AudioPlayerService");

        try
        {
            LibVLCSharp.Shared.Core.Initialize();
            Logger.Debug("LibVLC core initialized");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to initialize LibVLC core: {ex}");
            throw;
        }

        _libVLC = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVLC);
        Logger.Debug("MediaPlayer and LibVLC instances created");

        // Wire up events with logging
        _mediaPlayer.Playing += (_, _) =>
        {
            Logger.Trace("MediaPlayer.Playing event triggered");
            PlaybackStarted?.Invoke();
        };

        _mediaPlayer.Paused += (_, _) =>
        {
            Logger.Trace("MediaPlayer.Paused event triggered");
            PlaybackPaused?.Invoke();
        };

        _mediaPlayer.Stopped += (_, _) =>
        {
            Logger.Trace("MediaPlayer.Stopped event triggered");
            PlaybackStopped?.Invoke();
        };

        _mediaPlayer.TimeChanged += (_, e) =>
        {
            Logger.Trace($"Time changed to {e.Time} ms");
            PositionChanged?.Invoke((int)e.Time);
        };

        _mediaPlayer.LengthChanged += (_, e) =>
        {
            Logger.Debug($"Media duration changed to {e.Length} ms");
            DurationChanged?.Invoke((int)e.Length);
        };

        _mediaPlayer.EndReached += OnMediaEndReached;

        Logger.Info("AudioPlayerService initialized successfully");
    }

    // Events
    private async void OnMediaEndReached(object? sender, EventArgs e)
    {
        Logger.Info("Media playback ended");
        MediaEnded?.Invoke();

        if (IsLooping && IsMediaReady && _currentMedia != null)
        {
            Logger.Debug("Looping enabled");
            Logger.Debug("Restarting playback after 50ms delay...");
            await Task.Delay(50);

            try
            {
                _mediaPlayer.Media = _currentMedia;
                _mediaPlayer.Play();
                Logger.Debug("Looped media restarted");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to restart media during loop: {ex}");
            }
        }
        else
        {
            Logger.Debug($"Looping: {IsLooping}, MediaReady: {IsMediaReady}, CurrentMedia: {_currentMedia != null}");
        }
    }

    // Methods
    public async Task PlayFileAsync(string path)
    {
        Logger.Info($"PlayFileAsync called with path: {path}");

        if (!File.Exists(path))
        {
            Logger.Warning($"Media file not found: {path}");
            return;
        }

        Stop();
        IsMediaReady = false;

        _currentMedia?.Dispose();
        Logger.Debug("Previous media disposed");

        try
        {
            _currentMedia = new Media(_libVLC, new Uri(path));
            Logger.Debug($"Media object created for: {path}");

            await _currentMedia.Parse(MediaParseOptions.ParseLocal);
            Logger.Debug("Media parsed successfully");
            
            CurrentMetadata = ExtractMetadata(_currentMedia, path);
            MetadataLoaded?.Invoke(CurrentMetadata);
            
            IsMediaReady = true;
            _mediaPlayer.Media = _currentMedia;
            MediaReady?.Invoke();
            Logger.Info("Media is ready (Invoking MediaReady event)");

            Play();
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load or parse media file '{path}': {ex}");
            IsMediaReady = false;
        }
    }

    private AudioMetadata ExtractMetadata(Media media, string filePath)
    {
        AudioMetadata metadata = new AudioMetadata
        {
            FilePath = filePath,
            Duration = media.Duration,
        };

        try
        {
            metadata.Title = media.Meta(MetadataType.Title) ?? string.Empty;
            metadata.Artist = media.Meta(MetadataType.Artist) ?? string.Empty;
            metadata.Album = media.Meta(MetadataType.Album) ?? string.Empty;
            metadata.AlbumArtist = media.Meta(MetadataType.AlbumArtist) ?? string.Empty;
            metadata.Genre = media.Meta(MetadataType.Genre) ?? string.Empty;
            
            // Extract album artwork
            using TagLib.File? tagLibFile = TagLib.File.Create(filePath);
            if (tagLibFile.Tag.Pictures?.Length > 0)
            {
                IPicture? image = tagLibFile.Tag.Pictures[0]; // First picture
                byte[]? imageData = image?.Data.Data;
                if (imageData is { Length: > 0 })
                {
                    try
                    {
                        using MemoryStream stream = new MemoryStream(imageData);
                        metadata.AlbumArt = new Bitmap(stream);
                        Logger.Debug($"Album artwork extracted ({imageData.Length} bytes, Type: {image?.Type})");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Failed to create artwork from data: {ex}");
                    }
                }
            }
            else
            {
                Logger.Debug("Couldn't find any embedded artwork");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error extracting metadata: {ex}");
        }
        
        return metadata;
    }

    public void Play()
    {
        if (!IsMediaReady)
        {
            Logger.Warning("Play() called but media is not ready");
            return;
        }

        Logger.Info("Starting playback");
        _mediaPlayer.Play();
    }

    public void Pause()
    {
        Logger.Info("Pausing playback");
        _mediaPlayer.Pause();
    }

    public void Stop()
    {
        Logger.Info("Stopping playback");
        _mediaPlayer.Stop();
        IsMediaReady = false;
        Logger.Debug("Playback stopped and media marked as not ready");
    }

    public void SeekTo(int positionMs)
    {
        if (!IsMediaReady)
        {
            Logger.Warning("SeekTo() called but media is not ready");
            return;
        }

        Logger.Info($"Seeking to position: {positionMs}ms");
        _mediaPlayer.Time = positionMs;
    }

    public int GetCurrentPosition()
    {
        return (int)_mediaPlayer.Time;
    }

    public int GetDuration()
    {
        return (int)_mediaPlayer.Length;
    }

    public void Dispose()
    {
        Logger.Info("Disposing AudioPlayerService");

        try
        {
            _mediaPlayer?.Stop();
            _mediaPlayer?.Dispose();
            Logger.Debug("MediaPlayer disposed");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error disposing MediaPlayer: {ex}");
        }

        try
        {
            _currentMedia?.Dispose();
            Logger.Debug("Current media disposed");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error disposing current media: {ex}");
        }

        try
        {
            _libVLC?.Dispose();
            Logger.Debug("LibVLC disposed");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error disposing LibVLC: {ex}");
        }

        Logger.Info("AudioPlayerService disposed");
    }
}