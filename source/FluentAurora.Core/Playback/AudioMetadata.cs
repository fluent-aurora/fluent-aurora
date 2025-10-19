using FluentAurora.Core.Logging;
using TagLib;
using File = TagLib.File;

namespace FluentAurora.Core.Playback;

public class AudioMetadata
{
    // Properties
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string AlbumArtist { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public string Year { get; set; } = string.Empty;
    public int? TrackNumber { get; set; }
    public int? TrackTotal { get; set; }
    public double? Duration { get; set; }
    public byte[]? ArtworkData { get; set; }
    public string? FilePath { get; set; }

    // Methods
    public string DisplayTitle => !string.IsNullOrWhiteSpace(Title) ? Path.GetFileNameWithoutExtension(Title) : "Unknown Title";

    public static string[] GetSupportedExtensions() => [".mp3", ".ogg", ".wav", ".flac"];

    public static AudioMetadata Extract(string filePath)
    {
        AudioMetadata metadata = new AudioMetadata
        {
            FilePath = filePath,
        };

        try
        {
            using File fileMetadata = File.Create(filePath);
            metadata.Title = fileMetadata.Tag.Title ?? Path.GetFileNameWithoutExtension(filePath);
            Logger.Debug($"Title: {metadata.Title}");
            metadata.Artist = fileMetadata.Tag.FirstPerformer ?? string.Empty;
            Logger.Debug($"Artist: {metadata.Artist}");
            metadata.Album = fileMetadata.Tag.Album ?? string.Empty;
            Logger.Debug($"Album: {metadata.Album}");
            metadata.AlbumArtist = fileMetadata.Tag.FirstAlbumArtist ?? string.Empty;
            Logger.Debug($"Album Artist: {metadata.AlbumArtist}");
            metadata.Genre = fileMetadata.Tag.FirstGenre ?? string.Empty;
            Logger.Debug($"Genre: {metadata.Genre}");
            metadata.Duration = fileMetadata.Properties.Duration.TotalMilliseconds;
            Logger.Debug($"Duration: {metadata.Duration} ms");

            // Extract album artwork
            if (fileMetadata.Tag.Pictures?.Length > 0)
            {
                IPicture? image = fileMetadata.Tag.Pictures[0];
                byte[]? imageData = image?.Data.Data;
                if (imageData is { Length: > 0 })
                {
                    metadata.ArtworkData = imageData;
                    Logger.Debug($"Album artwork extracted ({imageData.Length} bytes, Type: {image?.Type})");
                }
            }
            else
            {
                Logger.Debug("No embedded artwork found");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error extracting metadata: {ex}");
            metadata.Title = Path.GetFileNameWithoutExtension(filePath);
        }

        return metadata;
    }
}