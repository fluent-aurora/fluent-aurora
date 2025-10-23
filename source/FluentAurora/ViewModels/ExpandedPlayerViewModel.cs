using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAurora.Core.Indexer;
using FluentAurora.Core.Playback;
using FluentAurora.Services;

namespace FluentAurora.ViewModels;

public partial class ExpandedPlayerViewModel : CompactPlayerViewModel
{
    // Variables
    public partial class QueueItemViewModel : ObservableObject
    {
        // Properties
        public AudioMetadata Song { get; }
        public int Index { get; }
        [ObservableProperty] private bool isCurrentlyPlaying;
        public string Title => Song.Title;
        public string Artist => Song.Artist;
        public double? Duration => Song.Duration;

        // Constructor
        public QueueItemViewModel(AudioMetadata song, int index)
        {
            Song = song;
            Index = index;
        }
    }

    private readonly AudioPlayerService _audioPlayerService;
    [ObservableProperty] private bool isQueueVisible = false;
    [ObservableProperty] private int currentSongIndex;
    [ObservableProperty] private ObservableCollection<QueueItemViewModel> queueItems = [];

    // Constructors
    public ExpandedPlayerViewModel(AudioPlayerService audioPlayerService, PlaybackControlService playbackControlService, StoragePickerService storagePickerService) : base(audioPlayerService, playbackControlService, storagePickerService)
    {
        _audioPlayerService = audioPlayerService;
        RefreshQueueItems();
        _audioPlayerService.PlaybackStarted += OnPlaybackChanged;
        _audioPlayerService.PlaybackStopped += OnPlaybackChanged;
        _audioPlayerService.MediaEnded += OnPlaybackChanged;
        _audioPlayerService.QueueChanged += OnQueueChanged;
        _audioPlayerService.MetadataLoaded += OnMetadataLoaded;
        
        DatabaseManager.SongDeleted += filePath =>
        {
            RefreshQueueItems();
        };
    }

    // Methods
    private void RefreshQueueItems()
    {
        ObservableCollection<QueueItemViewModel> items = new ObservableCollection<QueueItemViewModel>();
        List<AudioMetadata> queue = _audioPlayerService.Queue;

        for (int i = 0; i < queue.Count; i++)
        {
            QueueItemViewModel item = new QueueItemViewModel(queue[i], i)
            {
                IsCurrentlyPlaying = i == _audioPlayerService.CurrentIndex
            };
            items.Add(item);
        }

        QueueItems = items;
    }

    private void UpdateCurrentlyPlayingStatus()
    {
        foreach (QueueItemViewModel item in QueueItems)
        {
            item.IsCurrentlyPlaying = item.Index == CurrentSongIndex;
        }
    }

    // Events
    private void OnQueueChanged()
    {
        RefreshQueueItems();
    }

    private void OnPlaybackChanged()
    {
        CurrentSongIndex = _audioPlayerService.CurrentIndex;
        UpdateCurrentlyPlayingStatus();
    }

    private void OnMetadataLoaded(AudioMetadata metadata)
    {
        // Update currently playing status when the metadata loads
        UpdateCurrentlyPlayingStatus();
    }

    // Commands
    [RelayCommand]
    private void ToggleQueue()
    {
        IsQueueVisible = !IsQueueVisible;
    }

    [RelayCommand]
    private void PlayQueueItem(QueueItemViewModel item)
    {
        if (item?.Song == null)
        {
            return;
        }
        _audioPlayerService.PlayQueue(item.Index);
    }

    [RelayCommand]
    private void RemoveSong(QueueItemViewModel item)
    {
        if (item?.Song == null || item.Song.FilePath == null)
        {
            return;
        }
        _audioPlayerService.RemoveFromQueue(item.Song.FilePath);
    }
}