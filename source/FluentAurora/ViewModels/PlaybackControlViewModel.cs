using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAurora.Core.Playback;

namespace FluentAurora.ViewModels;

public partial class PlaybackControlViewModel : ViewModelBase
{
    // Properties
    private readonly AudioPlayerService _audioPlayerService;
    private bool _isUserSeeking = false;
    private bool _isDragging = false;
    private int _seekPosition;

    [ObservableProperty] private string songTitle = "No Song Selected";
    [ObservableProperty] private string songArtist = "Artists";
    [ObservableProperty] private int songDuration;
    [ObservableProperty] private int currentPosition;
    [ObservableProperty] private int displayPosition; // For showing while seeking
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
    public PlaybackControlViewModel()
    {
        _audioPlayerService = new AudioPlayerService();
        _audioPlayerService.Volume = CurrentVolume;

        _audioPlayerService.PlaybackStarted += () =>
        {
            Dispatcher.UIThread.Post(() => IsPlaying = true);
        };

        _audioPlayerService.PlaybackPaused += () =>
            Dispatcher.UIThread.Post(() => IsPlaying = false);

        _audioPlayerService.PlaybackStopped += () =>
            Dispatcher.UIThread.Post(() => IsPlaying = false);

        _audioPlayerService.PositionChanged += posMs =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                // Only update position if user is not seeking
                if (!_isUserSeeking)
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
            _ => true // RepeatMode.All & RepeatMode.On
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
    }

    public void PrepareForPotentialSeeking()
    {
        // Called on pointer press - prepare but don't start seeking yet
        _isDragging = false;
    }

    public void StartDragging()
    {
        // Called when actual dragging starts
        if (!_isDragging)
        {
            _isDragging = true;
            _isUserSeeking = true;
            _seekPosition = DisplayPosition;
        }
    }

    public void EndInteraction()
    {
        // Called on pointer release
        if (_isDragging)
        {
            // Dragging, seek to the dragged position
            _isUserSeeking = false;
            _isDragging = false;
            _audioPlayerService.SeekTo(_seekPosition);
            CurrentPosition = _seekPosition;
        }
        else
        {
            // Click, seek to clicked position immediately
            _audioPlayerService.SeekTo(DisplayPosition);
            CurrentPosition = DisplayPosition;
        }
    }

    [RelayCommand]
    private void TogglePlay() => IsPlaying = !IsPlaying;

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
            SongTitle = System.IO.Path.GetFileNameWithoutExtension(filePath);
            await _audioPlayerService.PlayFileAsync(filePath);
        }
    }
}