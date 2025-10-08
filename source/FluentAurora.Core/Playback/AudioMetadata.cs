using Avalonia.Media.Imaging;
using NLog.LayoutRenderers.Wrappers;

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
    public long Duration { get; set; }
    public Bitmap? AlbumArt { get; set; }
    public string? FilePath { get; set; }

    // Methods
    public string DisplayTitle => !string.IsNullOrWhiteSpace(Title) ? Path.GetFileNameWithoutExtension(Title) : "Unknown Title";
}