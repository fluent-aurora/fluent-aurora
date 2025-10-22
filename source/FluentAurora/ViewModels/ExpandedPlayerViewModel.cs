using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAurora.Core.Playback;
using FluentAurora.Services;

namespace FluentAurora.ViewModels;

public partial class ExpandedPlayerViewModel : CompactPlayerViewModel
{
    // Variables
    private readonly AudioPlayerService _audioPlayerService;
    [ObservableProperty] private bool isQueueVisible = false;
    [ObservableProperty] private int currentSongIndex;
    [ObservableProperty] public ObservableCollection<AudioMetadata> queue;

    // Constructors
    public ExpandedPlayerViewModel(AudioPlayerService audioPlayerService, PlaybackControlService playbackControlService, StoragePickerService storagePickerService) : base(audioPlayerService, playbackControlService, storagePickerService)
    {
        _audioPlayerService = audioPlayerService;
        queue = new ObservableCollection<AudioMetadata>(_audioPlayerService.Queue);
        _audioPlayerService.PlaybackStarted += OnPlaybackChanged;
        _audioPlayerService.PlaybackStopped += OnPlaybackChanged;
        _audioPlayerService.MediaEnded += OnPlaybackChanged;
        _audioPlayerService.QueueChanged += OnQueueChanged;
    }

    // Methods
    // Events
    private void OnQueueChanged()
    {
        Queue = new ObservableCollection<AudioMetadata>(_audioPlayerService.Queue);
        OnPropertyChanged(nameof(Queue));
    }

    private void OnPlaybackChanged()
    {
        CurrentSongIndex = _audioPlayerService.CurrentIndex;
    }

    // Commands
    [RelayCommand]
    private void ToggleQueue()
    {
        IsQueueVisible = !IsQueueVisible;
    }

    [RelayCommand]
    private void PlayQueueItem(AudioMetadata song)
    {
        if (song == null)
        {
            return;
        }

        int index = Queue.IndexOf(song);
        if (index >= 0)
        {
            _audioPlayerService.PlayQueue(index);
        }
    }
}