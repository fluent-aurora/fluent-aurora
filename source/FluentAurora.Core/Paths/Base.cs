namespace FluentAurora.Core.Paths;

public abstract class Base
{
    protected static string _baseDirectory => AppDomain.CurrentDomain.BaseDirectory;
    protected static string GetFullPath(string relativePath) => Path.Combine(_baseDirectory, relativePath);
    protected static string GetFullPath(params string[] relativePaths) => Path.Combine(new[] { _baseDirectory }.Concat(relativePaths).ToArray());
}