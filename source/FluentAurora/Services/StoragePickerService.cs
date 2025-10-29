using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace FluentAurora.Services;

public class StoragePickerService
{
    // Variables
    private static IStorageProvider StorageProvider => App.MainWindow?.StorageProvider ?? throw new InvalidOperationException("StorageProvider not found");

    private static FilePickerOpenOptions CreateAudioFilePickerOptions(string title = "Select Audio File", bool allowMultiple = false)
    {
        return new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = allowMultiple,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new FilePickerFileType("Audio Files")
                {
                    Patterns = ["*.mp3", "*.wav", "*.flac", "*.ogg"]
                }
            }
        };
    }

    private static FilePickerOpenOptions CreateImageFilePickerOptions(string title = "Select Image File", bool allowMultiple = false)
    {
        return new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = allowMultiple,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new FilePickerFileType("Image Files")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp"],
                    MimeTypes = ["image/*"]
                },
                new FilePickerFileType("All Files")
                {
                    Patterns = ["*.*"]
                }
            }
        };
    }

    private static FolderPickerOpenOptions CreateFolderPickerOptions(string title = "Select Folder", bool allowMultiple = false)
    {
        return new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = allowMultiple,
        };
    }

    // Methods
    public async Task<string?> PickAudioFileAsync() => await PickAudioFilesAsync(allowMultiple: false).ContinueWith(file => file.Result.FirstOrDefault());

    public async Task<IReadOnlyList<string>> PickAudioFilesAsync(bool allowMultiple = true)
    {
        string title = allowMultiple ? "Select Audio Files" : "Select Audio File";
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(CreateAudioFilePickerOptions(title: title, allowMultiple: allowMultiple));
        return files.Select(f => f.Path.LocalPath).ToList();
    }

    public async Task<string?> PickImageFileAsync() => await PickImageFilesAsync(allowMultiple: false).ContinueWith(file => file.Result.FirstOrDefault());

    public async Task<IReadOnlyList<string>> PickImageFilesAsync(bool allowMultiple = true)
    {
        string title = allowMultiple ? "Select Image Files" : "Select Image File";
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(CreateImageFilePickerOptions(title: title, allowMultiple: allowMultiple));
        return files.Select(f => f.Path.LocalPath).ToList();
    }

    public async Task<string?> PickFolderAsync() => await PickFoldersAsync(allowMultiple: false).ContinueWith(folder => folder.Result.FirstOrDefault());

    public async Task<IReadOnlyList<string>> PickFoldersAsync(bool allowMultiple = true)
    {
        string title = allowMultiple ? "Select Folders" : "Select Folder";
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(CreateFolderPickerOptions(title: title, allowMultiple: allowMultiple));
        return folders.Select(f => f.Path.LocalPath).ToList();
    }
}