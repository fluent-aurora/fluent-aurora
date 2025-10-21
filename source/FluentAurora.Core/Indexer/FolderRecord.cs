namespace FluentAurora.Core.Indexer;

public class FolderRecord
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int SongCount { get; init; }
}