using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAurora.Core.Indexer;
using FluentAurora.Core.Playback;

namespace FluentAurora.ViewModels;

public partial class FolderPlaylistViewModel : ObservableObject
{
    private readonly DatabaseManager _databaseManager;
    private ObservableCollection<AudioMetadata>? _songs;
    private bool _isLoaded;

    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;

    [ObservableProperty] private bool isExpanded;

    public ObservableCollection<AudioMetadata> Songs
    {
        get
        {
            if (!_isLoaded && IsExpanded)
            {
                LoadSongs();
            }
            return _songs ??= new ObservableCollection<AudioMetadata>();
        }
    }

    [ObservableProperty] private int songCount;

    public FolderPlaylistViewModel(DatabaseManager databaseManager)
    {
        _databaseManager = databaseManager;
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && !_isLoaded)
        {
            LoadSongs();
        }
    }

    private void LoadSongs()
    {
        if (_isLoaded)
        {
            return;
        }

        List<AudioMetadata> songs = _databaseManager.GetSongsFolder(Path);
        _songs = new ObservableCollection<AudioMetadata>(songs);
        _isLoaded = true;
        OnPropertyChanged(nameof(Songs));
    }
}