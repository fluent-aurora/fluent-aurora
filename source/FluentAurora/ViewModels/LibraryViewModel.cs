using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAurora.Core.Indexer;
using FluentAurora.Core.Logging;
using FluentAurora.Core.Playback;
using FluentAurora.Services;

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

public partial class LibraryViewModel : ViewModelBase
{
    private readonly DatabaseManager _databaseManager;
    private readonly StoragePickerService _storagePickerService;
    private readonly AudioPlayerService _audioPlayerService;

    public ObservableCollection<FolderPlaylistViewModel> Playlists { get; } = [];

    [ObservableProperty] private FolderPlaylistViewModel? selectedPlaylist;

    public LibraryViewModel(DatabaseManager databaseManager, StoragePickerService storagePickerService, AudioPlayerService audioPlayerService)
    {
        _databaseManager = databaseManager;
        _storagePickerService = storagePickerService;
        _audioPlayerService = audioPlayerService;
        LoadFolders();
    }

    [RelayCommand]
    public async Task LoadFoldersAsync()
    {
        try
        {
            Logger.Info("Loading folders from the database...");

            Playlists.Clear();

            // Load folders with song count only, not the actual songs
            List<FolderRecord> folders = await Task.Run(() => _databaseManager.GetAllFolders());

            foreach (FolderRecord folder in folders)
            {
                FolderPlaylistViewModel playlistVm = new FolderPlaylistViewModel(_databaseManager)
                {
                    Name = folder.Name,
                    Path = folder.Path,
                    SongCount = folder.SongCount,
                    IsExpanded = false
                };

                Playlists.Add(playlistVm);
            }

            Logger.Info($"Loaded {Playlists.Count} folders");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load folders: {ex}");
        }
    }

    private void LoadFolders() => _ = LoadFoldersAsync();

    [RelayCommand]
    public async Task AddFolder()
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

                _databaseManager.AddSongs(audioFiles);
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to add folder: {ex}");
        }
        finally
        {
            await LoadFoldersAsync();
        }
    }

    [RelayCommand]
    public async Task PlaySong(AudioMetadata song)
    {
        if (song == null || string.IsNullOrEmpty(song.FilePath))
        {
            Logger.Warning("No song selected or file path is empty");
            return;
        }

        _audioPlayerService.ClearQueue();
        await _audioPlayerService.PlayFileAsync(song.FilePath);
    }

    [RelayCommand]
    public void EnqueueSong(AudioMetadata song)
    {
        _audioPlayerService.Enqueue(song);
    }

    [RelayCommand]
    public void PlayPlaylist(FolderPlaylistViewModel playlist)
    {
        if (playlist == null)
        {
            Logger.Warning("Playlist is null");
            return;
        }

        Logger.Info($"Queueing playlist: {playlist.Name}");

        _audioPlayerService.ClearQueue();

        // Load songs without artwork for playback
        List<AudioMetadata> songs = _databaseManager.GetSongsFolder(playlist.Path);
        _audioPlayerService.Enqueue(songs);
        _audioPlayerService.PlayQueue();
    }
}