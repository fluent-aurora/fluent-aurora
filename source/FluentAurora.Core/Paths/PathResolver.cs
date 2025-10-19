namespace FluentAurora.Core.Paths;

public class PathResolver : Base
{
    public static readonly string Base = _baseDirectory;
    public static readonly string LogFile = GetFullPath("fluentaurora.log");
    public static readonly string Database = GetFullPath("music.sqlite");
}