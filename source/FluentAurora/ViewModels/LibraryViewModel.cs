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

public class FolderPlaylistViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsExpanded { get; set; }
    public ObservableCollection<AudioMetadata> Songs { get; set; } = [];
    public int SongCount => Songs.Count;
}

public partial class LibraryViewModel : ViewModelBase
{
    // Properties
    private readonly DatabaseManager _databaseManager;
    private readonly StoragePickerService _storagePickerService;
    private readonly AudioPlayerService _audioPlayerService;

    public ObservableCollection<FolderPlaylistViewModel> Playlists { get; } = [];

    [ObservableProperty] private FolderPlaylistViewModel? selectedPlaylist;

    // Constructors
    public LibraryViewModel(DatabaseManager databaseManager, StoragePickerService storagePickerService, AudioPlayerService audioPlayerService)
    {
        _databaseManager = databaseManager;
        _storagePickerService = storagePickerService;
        _audioPlayerService = audioPlayerService;
        LoadFolders();
    }

    // Methods
    [RelayCommand]
    public void LoadFolders()
    {
        try
        {
            Logger.Info("Loading folders from the database...");

            Playlists.Clear();

            List<FolderRecord> folders = _databaseManager.GetAllFolders(); // We'll add this helper in DatabaseManager
            foreach (FolderRecord folder in folders)
            {
                List<AudioMetadata> songs = _databaseManager.GetSongsByFolder(folder.Path);

                FolderPlaylistViewModel playlistVm = new FolderPlaylistViewModel
                {
                    Name = folder.Name,
                    Path = folder.Path,
                    Songs = new ObservableCollection<AudioMetadata>(songs),
                    IsExpanded = false
                };

                Playlists.Add(playlistVm);
            }

            Logger.Info($"Loaded {Playlists.Count} folders with songs");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load folders: {ex}");
        }
    }


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

            string[] supportedExtensions = AudioMetadata.GetSupportedExtensions();
            List<string> audioFiles = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories)
                .Where(file => supportedExtensions.Any(extension => file.EndsWith(extension, StringComparison.OrdinalIgnoreCase))).ToList();

            if (audioFiles.Count == 0)
            {
                Logger.Error("No audio files found");
                return;
            }

            _databaseManager.AddSongs(audioFiles);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to add folder: {ex}");
        }
        finally
        {
            LoadFolders();
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
        if (playlist == null || playlist.Songs.Count == 0)
        {
            Logger.Warning("Playlist is empty or null");
            return;
        }

        Logger.Info($"Queueing playlist: {playlist.Name}");

        _audioPlayerService.ClearQueue();
        _audioPlayerService.Enqueue(playlist.Songs);
        _audioPlayerService.PlayQueue();
    }
}