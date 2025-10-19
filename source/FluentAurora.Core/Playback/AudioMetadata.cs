using FluentAurora.Core.Logging;
using ATL;

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
            Track fileMetadata = new Track(filePath);
            metadata.Title = fileMetadata.Title ?? Path.GetFileNameWithoutExtension(filePath);
            Logger.Debug($"Title: {metadata.Title}");
            metadata.Artist = fileMetadata.Artist ?? string.Empty;
            Logger.Debug($"Artist: {metadata.Artist}");
            metadata.Album = fileMetadata.Album ?? string.Empty;
            Logger.Debug($"Album: {metadata.Album}");
            metadata.AlbumArtist = fileMetadata.AlbumArtist ?? string.Empty;
            Logger.Debug($"AlbumArtist: {metadata.AlbumArtist}");
            metadata.Genre = fileMetadata.Genre ?? string.Empty;
            Logger.Debug($"Genre: {metadata.Genre}");
            metadata.Duration = fileMetadata.DurationMs;
            Logger.Debug($"Duration: {metadata.Duration}");

            if (fileMetadata.EmbeddedPictures.Count > 0)
            {
                PictureInfo image = fileMetadata.EmbeddedPictures[0];
                byte[] artworkData = image.PictureData;
                if (artworkData is { Length: > 0 })
                {
                    metadata.ArtworkData = artworkData;
                    Logger.Debug($"Album artwork extracted ({artworkData.Length} bytes, Type: {image.NativeFormat})");
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