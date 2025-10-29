namespace FluentAurora.Core.Indexer;

public class PlaylistRecord
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public byte[]? CustomArtwork { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int SongCount { get; set; }
    public double? TotalDuration { get; set; }
}