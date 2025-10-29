using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAurora.Core.Indexer;
using FluentAurora.Core.Logging;
using FluentAurora.Core.Playback;
using FluentAurora.Services;
using FluentAvalonia.UI.Controls;

namespace FluentAurora.ViewModels;

public enum ViewMode
{
    Folders,
    AllSongs,
    Playlists
}

public partial class LibraryViewModel : ViewModelBase
{
    private readonly DatabaseManager _databaseManager;
    private readonly StoragePickerService _storagePickerService;
    private readonly AudioPlayerService _audioPlayerService;
    private readonly PlaylistDialogService _playlistDialogService;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty] private ViewMode currentViewMode = ViewMode.Folders;

    public ObservableCollection<FolderPlaylistViewModel> Folders { get; } = [];
    [ObservableProperty] private FolderPlaylistViewModel? selectedFolder;

    public ObservableCollection<PlaylistViewModel> Playlists { get; } = [];
    [ObservableProperty] private PlaylistViewModel? selectedPlaylist;
    [ObservableProperty] private bool isPlaylistDetailsOpen = false;

    // Indexing progress properties
    [ObservableProperty] private bool isIndexing = false;
    [ObservableProperty] private int songsIndexed = 0;
    [ObservableProperty] private int totalSongsToIndex = 0;
    [ObservableProperty] private string indexingFolderName = string.Empty;

    [ObservableProperty] private ObservableCollection<AudioMetadata> displayedSongs = new ObservableCollection<AudioMetadata>();
    [ObservableProperty] private bool isLoadingSongs = false;
    [ObservableProperty] private bool isSearching = false;
    [ObservableProperty] private string searchQuery = string.Empty;
    [ObservableProperty] private int totalSongCount = 0;

    public LibraryViewModel(DatabaseManager databaseManager, StoragePickerService storagePickerService, AudioPlayerService audioPlayerService, PlaylistDialogService playlistDialogService)
    {
        _databaseManager = databaseManager;
        DatabaseManager.SongDeleted += async _ =>
        {
            switch (CurrentViewMode)
            {
                case ViewMode.Playlists:
                    await LoadPlaylistsAsync();
                    break;
                case ViewMode.Folders:
                    await LoadFoldersAsync();
                    break;
                case ViewMode.AllSongs:
                    await LoadSongsAsync();
                    break;
            }
        };
        DatabaseManager.FolderDeleted += async _ =>
        {
            switch (CurrentViewMode)
            {
                case ViewMode.Playlists:
                    await LoadPlaylistsAsync();
                    break;
                case ViewMode.Folders:
                    await LoadFoldersAsync();
                    break;
                case ViewMode.AllSongs:
                    await LoadSongsAsync();
                    break;
            }
        };
        DatabaseManager.SongsAdded += async () =>
        {
            switch (CurrentViewMode)
            {
                case ViewMode.Playlists:
                    await LoadPlaylistsAsync();
                    break;
                case ViewMode.Folders:
                    await LoadFoldersAsync();
                    break;
                case ViewMode.AllSongs:
                    await LoadSongsAsync();
                    break;
            }
        };
        _storagePickerService = storagePickerService;
        _audioPlayerService = audioPlayerService;
        _playlistDialogService = playlistDialogService;
        LoadFolders();
        LoadPlaylists();
    }

    public bool ShowFolders => CurrentViewMode == ViewMode.Folders;
    public bool ShowAllSongs => CurrentViewMode == ViewMode.AllSongs;
    public bool ShowPlaylists => CurrentViewMode == ViewMode.Playlists;

    public string ViewModeText => CurrentViewMode switch
    {
        ViewMode.Folders => "Folders",
        ViewMode.AllSongs => "All Songs",
        ViewMode.Playlists => "Playlists",
        _ => "Folders"
    };

    partial void OnCurrentViewModeChanged(ViewMode value)
    {
        OnPropertyChanged(nameof(ShowFolders));
        OnPropertyChanged(nameof(ShowAllSongs));
        OnPropertyChanged(nameof(ShowPlaylists));
        OnPropertyChanged(nameof(ViewModeText));

        switch (value)
        {
            case ViewMode.AllSongs:
                _ = LoadSongsAsync();
                break;
            case ViewMode.Playlists:
                LoadPlaylists();
                break;
            case ViewMode.Folders:
                LoadFolders();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value), value, null);
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        _ = PerformSearchAsync();
    }

    private async Task PerformSearchAsync()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        CancellationToken token = _searchCts.Token;

        try
        {
            int delay = SearchQuery.Length switch
            {
                0 => 0, // Instant (Used for clearing search)
                1 => 300, // Single Character, too many results, the biggest delay
                2 => 200, // 2 characters
                _ => 125 // 3+ characters should have fast response
            };
            if (delay > 0)
            {
                await Task.Delay(delay, token);
            }
            await LoadSongsAsync();
        }
        catch (TaskCanceledException)
        {
            // Search cancelled, continue
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    [RelayCommand]
    private async Task LoadSongsAsync()
    {
        try
        {
            IsSearching = true;
            Logger.Debug($"Loading songs with search query: '{SearchQuery}'");

            List<AudioMetadata> songs = await Task.Run(() => _databaseManager.SearchSongs(SearchQuery));
            int totalCount = await Task.Run(() => _databaseManager.GetSongCount(null));

            DisplayedSongs.Clear();
            foreach (AudioMetadata song in songs)
            {
                DisplayedSongs.Add(song);
            }

            TotalSongCount = totalCount;

            Logger.Info($"Loaded {DisplayedSongs.Count} songs" + (string.IsNullOrWhiteSpace(SearchQuery) ? "" : $" (filtered from {totalCount})"));
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load songs: {ex}");
        }
        finally
        {
            IsSearching = false;
            IsLoadingSongs = false;
        }
    }

    [RelayCommand]
    private void CycleView()
    {
        CurrentViewMode = CurrentViewMode switch
        {
            ViewMode.Folders => ViewMode.AllSongs,
            ViewMode.AllSongs => ViewMode.Playlists,
            ViewMode.Playlists => ViewMode.Folders,
            _ => throw new ArgumentOutOfRangeException("Unknown view mode")
        };
    }

    [RelayCommand]
    private async Task LoadFoldersAsync()
    {
        try
        {
            Logger.Info("Loading folders from the database...");

            Folders.Clear();

            // Load folders with song count only, not the actual songs
            List<FolderRecord> folders = await Task.Run(() => _databaseManager.GetAllFolders());

            foreach (FolderRecord folder in folders)
            {
                FolderPlaylistViewModel folderVm = new FolderPlaylistViewModel(_databaseManager)
                {
                    Name = folder.Name,
                    Path = folder.Path,
                    SongCount = folder.SongCount,
                    IsExpanded = false
                };

                Folders.Add(folderVm);
            }

            Logger.Info($"Loaded {Folders.Count} folders");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load folders: {ex}");
        }
    }

    private void LoadFolders() => _ = LoadFoldersAsync();

    [RelayCommand]
    private async Task LoadPlaylistsAsync()
    {
        try
        {
            Logger.Info("Loading playlists from the database...");

            Playlists.Clear();

            List<PlaylistRecord> playlists = await Task.Run(() => _databaseManager.GetAllPlaylists());

            foreach (PlaylistRecord playlist in playlists)
            {
                PlaylistViewModel playlistVm = new PlaylistViewModel(_databaseManager)
                {
                    Id = playlist.Id,
                    Name = playlist.Name,
                    SongCount = playlist.SongCount,
                    CustomArtwork = playlist.CustomArtwork,
                    CreatedAt = playlist.CreatedAt,
                    UpdatedAt = playlist.UpdatedAt,
                    TotalDuration = playlist.TotalDuration,
                    IsExpanded = false
                };

                Playlists.Add(playlistVm);
            }

            await Task.Run(() =>
            {
                foreach (var playlist in Playlists)
                {
                    try
                    {
                        playlist.LoadArtworkImmediate();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to load artwork for playlist {playlist.Name}: {ex}");
                    }
                }
            });

            Logger.Info($"Loaded {Playlists.Count} playlists with artwork");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load playlists: {ex}");
        }
    }

    private void LoadPlaylists() => _ = LoadPlaylistsAsync();

    [RelayCommand]
    private async Task AddFolder()
    {
        try
        {
            string? folderPath = await _storagePickerService.PickFolderAsync();
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                Logger.Error("No folder selected or the selected folder does not exist");
                return;
            }

            Logger.Debug($"Indexing folder: {folderPath}");
            IndexingFolderName = Path.GetFileName(folderPath);

            // Run indexing in background
            await Task.Run(() =>
            {
                string[] supportedExtensions = AudioMetadata.GetSupportedExtensions();
                List<string> audioFiles = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories)
                    .Where(file => supportedExtensions.Any(extension =>
                        file.EndsWith(extension, StringComparison.OrdinalIgnoreCase))).ToList();

                if (audioFiles.Count == 0)
                {
                    Logger.Error("No audio files found");
                    return;
                }

                IsIndexing = true;
                TotalSongsToIndex = audioFiles.Count;
                SongsIndexed = 0;

                // Indexing batch size to make indexing faster
                const int BATCH_SIZE = 10;
                for (int i = 0; i < audioFiles.Count; i += BATCH_SIZE)
                {
                    int currentBatchSize = Math.Min(BATCH_SIZE, audioFiles.Count - i);
                    List<string> batch = audioFiles.GetRange(i, currentBatchSize);

                    _databaseManager.AddSongs(batch);

                    SongsIndexed += currentBatchSize;
                    Logger.Debug($"Indexed {SongsIndexed}/{TotalSongsToIndex} songs");
                }
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to add folder: {ex}");
        }
        finally
        {
            IsIndexing = false;
            await LoadFoldersAsync();
        }
    }

    [RelayCommand]
    private void PlayAllSongs()
    {
        if (DisplayedSongs.Count == 0)
        {
            Logger.Warning("No songs to play");
            return;
        }

        Logger.Info("Playing all songs");
        _audioPlayerService.ClearQueue();
        _audioPlayerService.Enqueue(DisplayedSongs.ToList());
        _audioPlayerService.PlayQueue();
    }

    [RelayCommand]
    private void PlayAllSongsShuffled()
    {
        if (DisplayedSongs.Count == 0)
        {
            Logger.Warning("No songs to play");
            return;
        }

        Logger.Info("Playing all songs shuffled");
        _audioPlayerService.IsShuffled = false;
        _audioPlayerService.ClearQueue();
        _audioPlayerService.Enqueue(DisplayedSongs.ToList());
        _audioPlayerService.ToggleShuffle();
        _audioPlayerService.PlayQueue();
    }

    [RelayCommand]
    private void PlayFolder(FolderPlaylistViewModel folder)
    {
        if (folder == null)
        {
            Logger.Warning("Folder is null");
            return;
        }

        Logger.Info($"Queueing Folder: {folder.Name}");

        _audioPlayerService.ClearQueue();

        // Load songs without artwork for playback
        List<AudioMetadata> songs = _databaseManager.GetSongsFolder(folder.Path);
        _audioPlayerService.Enqueue(songs);
        _audioPlayerService.PlayQueue();
    }

    [RelayCommand]
    private void PlayFolderShuffled(FolderPlaylistViewModel folder)
    {
        if (folder == null)
        {
            Logger.Warning("Folder is null");
            return;
        }

        Logger.Info($"Queueing Folder: {folder.Name} (Shuffled)");
        _audioPlayerService.IsShuffled = false; // Reset shuffle state
        _audioPlayerService.ClearQueue();

        // Load songs without artwork for playback
        List<AudioMetadata> songs = _databaseManager.GetSongsFolder(folder.Path);
        _audioPlayerService.Enqueue(songs);
        _audioPlayerService.ToggleShuffle();
        _audioPlayerService.PlayQueue();
    }

    [RelayCommand]
    private void DeleteFolder(FolderPlaylistViewModel folder)
    {
        if (folder == null)
        {
            Logger.Warning("Folder is null");
            return;
        }
        Logger.Info($"Deleting folder: {folder.Name}");
        DatabaseManager.DeleteFolder(folder.Path);
    }

    [RelayCommand]
    private async Task PlaySong(object? parameter)
    {
        AudioMetadata? song = parameter switch
        {
            AudioMetadata audioMetadata => audioMetadata,
            PlaylistSongViewModel playlistSong => playlistSong.Song,
            _ => null
        };

        if (song == null || string.IsNullOrEmpty(song.FilePath))
        {
            Logger.Warning("No song selected or file path is empty");
            return;
        }

        _audioPlayerService.ClearQueue();

        // Playlist
        if (parameter is PlaylistSongViewModel playlistSongVm)
        {
            Logger.Info($"Playing song '{song.Title}' from playlist '{playlistSongVm.Playlist.Name}'");

            List<AudioMetadata> playlistSongs = _databaseManager.GetPlaylistSongs(playlistSongVm.Playlist.Id);

            if (playlistSongs.Count == 0)
            {
                Logger.Warning("Playlist has no songs");
                return;
            }
            _audioPlayerService.Enqueue(playlistSongs);

            // Trying to find the song index
            // If there is none, play from start
            int songIndex = playlistSongs.FindIndex(s => s.FilePath == song.FilePath);
            if (songIndex >= 0)
            {
                _audioPlayerService.PlayQueue(songIndex);
            }
            else
            {
                Logger.Warning($"Could not find song in playlist, playing from start");
                _audioPlayerService.PlayQueue();
            }

            return;
        }

        // All Songs View will play all the displayed songs
        if (CurrentViewMode == ViewMode.AllSongs && DisplayedSongs.Count > 0)
        {
            Logger.Info($"Playing song '{song.Title}' from All Songs view");

            _audioPlayerService.Enqueue(DisplayedSongs.ToList());

            int songIndex = DisplayedSongs.ToList().FindIndex(s => s.FilePath == song.FilePath);

            if (songIndex >= 0)
            {
                _audioPlayerService.PlayQueue(songIndex);
            }
            else
            {
                Logger.Warning($"Could not find song in displayed songs, playing from start");
                _audioPlayerService.PlayQueue();
            }

            return;
        }

        // Other scenarios play just 1 song
        try
        {
            await _audioPlayerService.PlayFileAsync(song.FilePath);
        }
        catch (FileNotFoundException)
        {
            Logger.Error($"File not found: {song.FilePath}");
        }
    }

    [RelayCommand]
    private void EnqueueSong(object? parameter)
    {
        AudioMetadata? song = parameter switch
        {
            AudioMetadata audioMetadata => audioMetadata,
            PlaylistSongViewModel playlistSong => playlistSong.Song,
            _ => null
        };

        if (song != null)
        {
            _audioPlayerService.Enqueue(song);
        }
    }

    [RelayCommand]
    private async Task RemoveSong(object? parameter)
    {
        AudioMetadata? song = parameter switch
        {
            AudioMetadata audioMetadata => audioMetadata,
            PlaylistSongViewModel playlistSong => playlistSong.Song,
            _ => null
        };

        if (song?.FilePath != null)
        {
            await Task.Run(() => DatabaseManager.DeleteSong(song.FilePath));
        }
    }

    [RelayCommand]
    private async Task CreatePlaylist()
    {
        try
        {
            long? playlistId = await _playlistDialogService.ShowCreatePlaylistDialogAsync();

            if (playlistId.HasValue)
            {
                await LoadPlaylistsAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to create playlist with song: {ex}");
            await _playlistDialogService.ShowErrorDialogAsync("Failed to Create Playlist", "An error occurred while creating the playlist.");
        }
    }

    [RelayCommand]
    private void PlayPlaylist(PlaylistViewModel playlist)
    {
        if (playlist == null)
        {
            Logger.Warning("Playlist is null");
            return;
        }

        Logger.Info($"Playing playlist: {playlist.Name}");
        _audioPlayerService.ClearQueue();

        List<AudioMetadata> songs = _databaseManager.GetPlaylistSongs(playlist.Id);
        _audioPlayerService.Enqueue(songs);
        _audioPlayerService.PlayQueue();
    }

    [RelayCommand]
    private void PlayPlaylistShuffled(PlaylistViewModel playlist)
    {
        if (playlist == null)
        {
            Logger.Warning("Playlist is null");
            return;
        }

        Logger.Info($"Playing playlist shuffled: {playlist.Name}");
        _audioPlayerService.IsShuffled = false;
        _audioPlayerService.ClearQueue();

        List<AudioMetadata> songs = _databaseManager.GetPlaylistSongs(playlist.Id);
        _audioPlayerService.Enqueue(songs);
        _audioPlayerService.ToggleShuffle();
        _audioPlayerService.PlayQueue();
    }

    [RelayCommand]
    private void DeletePlaylist(PlaylistViewModel playlist)
    {
        if (playlist == null)
        {
            Logger.Warning("Playlist is null");
            return;
        }

        Logger.Info($"Deleting playlist: {playlist.Name}");
        DatabaseManager.DeletePlaylist(playlist.Id);
        LoadPlaylists();
    }

    [RelayCommand]
    private async Task AddSongToPlaylist(object? parameter)
    {
        AudioMetadata? song = parameter switch
        {
            AudioMetadata audioMetadata => audioMetadata,
            PlaylistSongViewModel playlistSong => playlistSong.Song,
            _ => null
        };

        if (song == null || string.IsNullOrEmpty(song.FilePath))
        {
            Logger.Warning("Cannot add to playlist: song or file path is null");
            return;
        }

        try
        {
            long? playlistId = await _playlistDialogService.ShowPlaylistSelectionDialogAsync(song.Title);

            if (playlistId.HasValue)
            {
                DatabaseManager.AddSongToPlaylist(playlistId.Value, song.FilePath);
                Logger.Info($"Added '{song.Title}' to playlist");
                if (CurrentViewMode == ViewMode.Playlists)
                {
                    await LoadPlaylistsAsync();
                }

                if (SelectedPlaylist != null && SelectedPlaylist.Id == playlistId.Value)
                {
                    SelectedPlaylist.RefreshDuration();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to add song to playlist: {ex}");
            await _playlistDialogService.ShowErrorDialogAsync("Failed to Add Song", "An error occurred while adding the song to the playlist.");
        }
    }

    [RelayCommand]
    private async Task ChangePlaylistArtwork(PlaylistViewModel playlist)
    {
        if (playlist == null)
        {
            Logger.Warning("Cannot change artwork: playlist is null");
            return;
        }

        try
        {
            string? imagePath = await _storagePickerService.PickImageFileAsync();

            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                Logger.Debug("No image selected or file doesn't exist");
                return;
            }

            byte[] imageData = await File.ReadAllBytesAsync(imagePath);
            const int MAX_SIZE = 5 * 1024 * 1024;
            if (imageData.Length > MAX_SIZE)
            {
                await _playlistDialogService.ShowErrorDialogAsync("Image Too Large", "The selected image is too large. Please choose an image smaller than 5MB.");
                return;
            }

            DatabaseManager.UpdatePlaylistArtwork(playlist.Id, imageData);
            playlist.CustomArtwork = imageData;
            await playlist.LoadArtworkAsync();

            Logger.Info($"Updated artwork for playlist '{playlist.Name}'");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to change playlist artwork: {ex}");
            await _playlistDialogService.ShowErrorDialogAsync("Failed to Change Artwork", "An error occurred while updating the playlist artwork.");
        }
    }

    [RelayCommand]
    private async Task RemovePlaylistArtwork(PlaylistViewModel playlist)
    {
        if (playlist == null)
        {
            return;
        }

        try
        {
            DatabaseManager.UpdatePlaylistArtwork(playlist.Id, null);
            playlist.CustomArtwork = null;
            await playlist.LoadArtworkAsync();

            Logger.Info($"Removed custom artwork for playlist '{playlist.Name}'");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to remove playlist artwork: {ex}");
        }
    }

    [RelayCommand]
    private async Task CreatePlaylistWithSong(object? parameter)
    {
        AudioMetadata? song = parameter switch
        {
            AudioMetadata audioMetadata => audioMetadata,
            PlaylistSongViewModel playlistSong => playlistSong.Song,
            _ => null
        };

        if (song == null || string.IsNullOrEmpty(song.FilePath))
        {
            Logger.Warning("Cannot create playlist: song or file path is null");
            return;
        }

        try
        {
            long? playlistId = await _playlistDialogService.ShowCreatePlaylistDialogAsync();

            if (playlistId.HasValue)
            {
                DatabaseManager.AddSongToPlaylist(playlistId.Value, song.FilePath);
                Logger.Info($"Created playlist and added '{song.Title}'");
                if (CurrentViewMode == ViewMode.Playlists)
                {
                    await LoadPlaylistsAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to create playlist with song: {ex}");
            await _playlistDialogService.ShowErrorDialogAsync("Failed to Create Playlist", "An error occurred while creating the playlist.");
        }
    }

    [RelayCommand]
    private async Task RenamePlaylist(PlaylistViewModel playlist)
    {
        if (playlist == null)
        {
            Logger.Warning("Cannot rename: playlist is null");
            return;
        }

        try
        {
            string? newName = await _playlistDialogService.ShowRenamePlaylistDialogAsync(playlist.Name);

            if (newName != null && newName != playlist.Name)
            {
                try
                {
                    DatabaseManager.RenamePlaylist(playlist.Id, newName);
                    playlist.Name = newName;
                    Logger.Info($"Renamed playlist to: {newName}");
                }
                catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19)
                {
                    Logger.Error($"Failed to rename playlist: Name already exists");
                    await _playlistDialogService.ShowErrorDialogAsync("Failed to Rename Playlist", $"A playlist named \"{newName}\" already exists. Please choose a different name.");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to rename playlist: {ex}");
                    await _playlistDialogService.ShowErrorDialogAsync("Failed to Rename Playlist", "An unexpected error occurred while renaming the playlist.");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error showing rename dialog: {ex}");
        }
    }

    [RelayCommand]
    private async Task RemoveFromPlaylist(PlaylistSongViewModel? playlistSong)
    {
        if (playlistSong?.Playlist == null || playlistSong.Song == null || string.IsNullOrEmpty(playlistSong.Song.FilePath))
        {
            Logger.Warning("Cannot remove from playlist: invalid parameters");
            return;
        }

        try
        {
            await Task.Run(() =>
            {
                DatabaseManager.RemoveSongFromPlaylist(playlistSong.Playlist.Id, playlistSong.Song.FilePath);
                playlistSong.Playlist.RemoveSong(playlistSong);
            });
            playlistSong.Playlist.RefreshDuration();
            Logger.Info($"Removed '{playlistSong.Song.Title}' from playlist '{playlistSong.Playlist.Name}'");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to remove song from playlist: {ex}");
        }
    }

    [RelayCommand]
    private void OpenPlaylistDetails(PlaylistViewModel playlist)
    {
        if (playlist == null)
        {
            return;
        }

        SelectedPlaylist = playlist;
        if (!playlist.IsExpanded)
        {
            playlist.IsExpanded = true; // This triggers LoadSongs()
        }

        IsPlaylistDetailsOpen = true; // Show the dialog
    }

    [RelayCommand]
    private async Task DeletePlaylistFromDetails(PlaylistViewModel playlist)
    {
        if (playlist == null)
        {
            Logger.Warning("Playlist is null");
            return;
        }

        bool result = await _playlistDialogService.ShowConfirmationDialogAsync("Delete Playlist", $"Are you sure you want to delete \"{playlist.Name}\"? This action cannot be undone.");
        if (result)
        {
            // Delete the playlist and update the UI
            Logger.Info($"Deleting playlist: {playlist.Name}");
            DatabaseManager.DeletePlaylist(playlist.Id);
            ClosePlaylistDetails();
            await LoadPlaylistsAsync();
        }
    }

    [RelayCommand]
    private void ClosePlaylistDetails()
    {
        IsPlaylistDetailsOpen = false;
        SelectedPlaylist = null;
    }

    [RelayCommand]
    private async Task MovePlaylistSongUp(PlaylistSongViewModel? playlistSong)
    {
        if (playlistSong?.Playlist == null || SelectedPlaylist == null)
        {
            return;
        }

        int currentIndex = SelectedPlaylist.PlaylistSongs.IndexOf(playlistSong);
        if (currentIndex <= 0)
        {
            Logger.Debug("Cannot move song up: already at the top");
            return;
        }

        try
        {
            // Move in UI
            SelectedPlaylist.PlaylistSongs.Move(currentIndex, currentIndex - 1);

            // Update database
            await Task.Run(() =>
            {
                List<string> songPaths = SelectedPlaylist.PlaylistSongs
                    .Select(ps => ps.Song.FilePath!)
                    .Where(path => !string.IsNullOrEmpty(path))
                    .ToList();

                DatabaseManager.UpdatePlaylistSongPositions(SelectedPlaylist.Id, songPaths);
            });

            Logger.Info($"Moved '{playlistSong.Song.Title}' up in playlist '{SelectedPlaylist.Name}'");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to move song up: {ex}");
        }
    }

    [RelayCommand]
    private async Task MovePlaylistSongDown(PlaylistSongViewModel? playlistSong)
    {
        if (playlistSong?.Playlist == null || SelectedPlaylist == null)
        {
            return;
        }

        int currentIndex = SelectedPlaylist.PlaylistSongs.IndexOf(playlistSong);
        if (currentIndex < 0 || currentIndex >= SelectedPlaylist.PlaylistSongs.Count - 1)
        {
            Logger.Debug("Cannot move song down: already at the bottom");
            return;
        }

        try
        {
            // Move in UI
            SelectedPlaylist.PlaylistSongs.Move(currentIndex, currentIndex + 1);

            // Update database
            await Task.Run(() =>
            {
                List<string> songPaths = SelectedPlaylist.PlaylistSongs
                    .Select(ps => ps.Song.FilePath!)
                    .Where(path => !string.IsNullOrEmpty(path))
                    .ToList();

                DatabaseManager.UpdatePlaylistSongPositions(SelectedPlaylist.Id, songPaths);
            });

            Logger.Info($"Moved '{playlistSong.Song.Title}' down in playlist '{SelectedPlaylist.Name}'");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to move song down: {ex}");
        }
    }
}