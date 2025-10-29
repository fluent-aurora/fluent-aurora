using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAurora.Core.Indexer;
using FluentAurora.Core.Playback;

namespace FluentAurora.ViewModels;

public partial class PlaylistViewModel : ObservableObject
{
    private readonly DatabaseManager _databaseManager;

    [ObservableProperty] private long id;
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private int songCount;
    [ObservableProperty] private bool isExpanded;
    [ObservableProperty] private ObservableCollection<PlaylistSongViewModel> playlistSongs = new();
    [ObservableProperty] private byte[]? artwork;
    [ObservableProperty] private byte[]? customArtwork;
    [ObservableProperty] private DateTime createdAt;
    [ObservableProperty] private DateTime updatedAt;
    [ObservableProperty] private double? totalDuration;

    public ObservableCollection<AudioMetadata> Songs => new ObservableCollection<AudioMetadata>(PlaylistSongs.Select(ps => ps.Song));

    public PlaylistViewModel(DatabaseManager databaseManager)
    {
        _databaseManager = databaseManager;
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && PlaylistSongs.Count == 0 && SongCount > 0)
        {
            LoadSongs();
        }
    }

    partial void OnTotalDurationChanged(double? value)
    {
        RefreshDuration();
    }

    public void LoadSongs()
    {
        List<AudioMetadata> loadedSongs = _databaseManager.GetPlaylistSongs(Id);
        PlaylistSongs.Clear();
        foreach (AudioMetadata song in loadedSongs)
        {
            PlaylistSongs.Add(new PlaylistSongViewModel(song, this));
        }
    }

    public void LoadArtworkImmediate()
    {
        // Don't load generated artwork if custom artwork is set
        if (CustomArtwork != null && CustomArtwork.Length > 0)
        {
            return;
        }

        // Only load if there are songs in the playlist
        if (SongCount > 0)
        {
            byte[]? artworkData = DatabaseManager.GetPlaylistArtwork(Id);

            Dispatcher.UIThread.Post(() =>
            {
                Artwork = artworkData;
            });
        }
    }

    public async Task LoadArtworkAsync()
    {
        await Task.Run(() => LoadArtworkImmediate());
    }


    public void RefreshSongCount()
    {
        SongCount = PlaylistSongs.Count;
    }

    public void RefreshDuration()
    {
        TotalDuration = DatabaseManager.GetPlaylistTotalDuration(Id);
    }

    public void RemoveSong(PlaylistSongViewModel playlistSong)
    {
        PlaylistSongs.Remove(playlistSong);
        RefreshSongCount();
    }
}

public partial class PlaylistSongViewModel : ObservableObject
{
    [ObservableProperty] private AudioMetadata song;
    [ObservableProperty] private PlaylistViewModel playlist;

    public PlaylistSongViewModel(AudioMetadata song, PlaylistViewModel playlist)
    {
        this.song = song;
        this.playlist = playlist;
    }

    public string Title => Song.Title;
    public string Artist => Song.Artist;
    public string Album => Song.Album;
    public double? Duration => Song.Duration;
    public string? FilePath => Song.FilePath;
}