using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAurora.Core.Logging;
using FluentAurora.Core.Playback;

namespace FluentAurora.ViewModels;

public partial class PlaybackControlViewModel : ViewModelBase
{
    // Properties
    private readonly AudioPlayerService _audioPlayerService;
    private bool _isUserSeeking = false;
    private bool _isDragging = false;
    private int _seekPosition;
    private int _clickSeekPosition = -1;
    private bool _suppressPositionUpdate = false;
    private CancellationTokenSource? _seekSuppressionCts;

    [ObservableProperty] private AudioMetadata? currentMetadata;
    public string SongTitle => CurrentMetadata?.DisplayTitle ?? "No Song Selected";
    public string SongArtist => CurrentMetadata?.Artist ?? string.Empty;
    public string SongAlbum => CurrentMetadata?.Album ?? string.Empty;
    [ObservableProperty] private Bitmap? songArtwork;
    [ObservableProperty] private int songDuration;
    [ObservableProperty] private int currentPosition;
    [ObservableProperty] private int displayPosition;
    [ObservableProperty] private int currentVolume = 100;
    [ObservableProperty] private bool isPlaying;

    public string PlayPauseIcon => IsPlaying ? "Pause" : "Play";

    [ObservableProperty] private RepeatMode repeatMode = RepeatMode.One;

    public string RepeatIcon => RepeatMode switch
    {
        RepeatMode.All => "ArrowRepeatAll",
        RepeatMode.One => "ArrowRepeat1",
        _ => "ArrowRepeatAllOff"
    };

    public string VolumeIcon => CurrentVolume switch
    {
        >= 1 and < 25 => "Speaker0",
        >= 26 and < 60 => "Speaker1",
        >= 61 and <= 100 => "Speaker2",
        _ => "SpeakerMute"
    };

    // Constructor
    public PlaybackControlViewModel(AudioPlayerService audioPlayerService)
    {
        _audioPlayerService = audioPlayerService;
        _audioPlayerService.Volume = CurrentVolume;

        _audioPlayerService.PlaybackStarted += () =>
        {
            Dispatcher.UIThread.Post(() => IsPlaying = true);
        };
        _audioPlayerService.PlaybackPaused += () => Dispatcher.UIThread.Post(() => IsPlaying = false);
        _audioPlayerService.PlaybackStopped += () => Dispatcher.UIThread.Post(() => IsPlaying = false);
        _audioPlayerService.PositionChanged += posMs =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                // Update position if user is not seeking and we're not suppressing updates
                if (!_isUserSeeking && !_suppressPositionUpdate)
                {
                    CurrentPosition = posMs;
                    DisplayPosition = posMs;
                }
            });
        };

        _audioPlayerService.DurationChanged += durMs =>
        {
            Dispatcher.UIThread.Post(() => SongDuration = durMs);
        };

        _audioPlayerService.MetadataLoaded += metadata =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                CurrentMetadata = metadata;
                if (CurrentMetadata.ArtworkData != null)
                {
                    try
                    {
                        using MemoryStream stream = new MemoryStream(CurrentMetadata.ArtworkData);
                        SongArtwork = new Bitmap(stream);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Failed to convert album art to bitmap: {ex}");
                        SongArtwork = null;
                    }
                }
                OnPropertyChanged(nameof(SongTitle));
                OnPropertyChanged(nameof(SongArtist));
                OnPropertyChanged(nameof(SongAlbum));
                OnPropertyChanged(nameof(SongArtwork));
            });
        };
    }

    // Methods
    partial void OnIsPlayingChanged(bool value)
    {
        OnPropertyChanged(nameof(PlayPauseIcon));
        if (value)
        {
            _audioPlayerService.Play();
        }
        else
        {
            _audioPlayerService.Pause();
        }
    }

    partial void OnRepeatModeChanged(RepeatMode value)
    {
        OnPropertyChanged(nameof(RepeatIcon));
        UpdateRepeatMode();
    }

    private void UpdateRepeatMode()
    {
        _audioPlayerService.IsLooping = RepeatMode switch
        {
            RepeatMode.Off => false,
            _ => true
        };
    }

    partial void OnCurrentVolumeChanged(int value)
    {
        OnPropertyChanged(nameof(VolumeIcon));
        _audioPlayerService.Volume = value;
    }

    partial void OnDisplayPositionChanged(int value)
    {
        // Track the position the user is dragging to
        if (_isUserSeeking)
        {
            _seekPosition = value;
        }
        else if (_clickSeekPosition == -1)
        {
            // Store the clicked position for potential click seek
            _clickSeekPosition = value;
        }
    }

    public void PrepareForPotentialSeeking()
    {
        // Prepare for potential seeking
        _isDragging = false;
        _isUserSeeking = true; // Block position updates immediately on potential seeking
        _clickSeekPosition = -1;
    }

    public void StartDragging()
    {
        // When actual dragging starts
        if (!_isDragging)
        {
            _isDragging = true;
            _isUserSeeking = true;
            _seekPosition = DisplayPosition;
        }
    }

    public async void EndInteraction()
    {
        // Called on pointer release
        int targetPosition;

        if (_isDragging)
        {
            // Seek to the dragged position
            targetPosition = _seekPosition;
        }
        else
        {
            // Used the stored click position/current display position
            targetPosition = _clickSeekPosition != -1 ? _clickSeekPosition : DisplayPosition;
        }

        // Suppress position updates to prevent flicker
        await SuppressPositionUpdatesAndSeek(targetPosition);

        _isDragging = false;
        _isUserSeeking = false;
        _clickSeekPosition = -1;
    }

    private async Task SuppressPositionUpdatesAndSeek(int targetPosition)
    {
        // Cancel previous suppression
        _seekSuppressionCts?.Cancel();
        _seekSuppressionCts = new System.Threading.CancellationTokenSource();

        // Enable suppression
        _suppressPositionUpdate = true;

        // Seek to the targeted position
        _audioPlayerService.SeekTo(targetPosition);
        CurrentPosition = targetPosition;
        DisplayPosition = targetPosition;

        try
        {
            // Keep suppression for short time to allow seeking to finish
            await Task.Delay(150, _seekSuppressionCts.Token);
        }
        catch (TaskCanceledException)
        {
            // Another seek happened, don't do anything
        }
        finally
        {
            // Re-enable position updating
            _suppressPositionUpdate = false;
        }
    }

    [RelayCommand]
    private void TogglePlay()
    {
        if (_audioPlayerService.IsMediaReady)
        {
            IsPlaying = !IsPlaying;
        }
        else
        {
            Logger.Warning("There's no file playing");
        }
    }

    [RelayCommand]
    private void ToggleRepeat()
    {
        RepeatMode = RepeatMode switch
        {
            RepeatMode.All => RepeatMode.One,
            RepeatMode.One => RepeatMode.Off,
            _ => RepeatMode.All
        };
    }

    [RelayCommand]
    private async Task OpenFile()
    {
        if (App.MainWindow?.StorageProvider is not { } storageProvider)
        {
            return;
        }

        IReadOnlyList<IStorageFile> result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Audio File",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new FilePickerFileType("Audio Files") { Patterns = ["*.mp3", "*.wav", "*.flac", "*.ogg"] }
            }
        });

        if (result.Count > 0)
        {
            string? filePath = result[0].Path.LocalPath;
            await _audioPlayerService.PlayFileAsync(filePath);
        }
    }
}