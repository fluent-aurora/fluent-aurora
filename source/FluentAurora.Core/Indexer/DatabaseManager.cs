using FluentAurora.Core.Logging;
using FluentAurora.Core.Paths;
using FluentAurora.Core.Playback;
using Microsoft.Data.Sqlite;

namespace FluentAurora.Core.Indexer;

public class DatabaseManager
{
    // Constants
    private const int MAX_ARTWORK_CACHE_SIZE = 100;

    // Properties
    public static string ConnectionString => $"Data Source={PathResolver.Database};Pooling=True";
    private static readonly ArtworkCache _artworkCache = new ArtworkCache();

    // Events
    public static event Action<string>? SongDeleted;
    public static event Action<string>? FolderDeleted;
    public static event Action<List<string>>? SongsDeleted;

    // Constructors
    public DatabaseManager()
    {
        EnsureDatabase();
    }

    // Methods
    // Initialization
    private void EnsureDatabase()
    {
        Logger.Info("Checking if the library database exists");
        bool dbExists = File.Exists(PathResolver.Database);

        using SqliteConnection connection = CreateConnection();
        connection.Open();

        if (!dbExists)
        {
            Logger.Warning("The library database does not exist — creating a new one");
            InitializeDatabase(connection);
        }
        else
        {
            Logger.Debug("Verifying all tables exist in the existing database");
            VerifyDatabaseSchema(connection);
        }

        CreateIndexes(connection);
    }

    private void InitializeDatabase(SqliteConnection connection)
    {
        Logger.Info("Creating library database schema...");
        ExecuteSchemaCommands(connection, DatabaseSchema.AllTables);
        Logger.Info("Database schema created successfully");
    }

    private void VerifyDatabaseSchema(SqliteConnection connection)
    {
        ExecuteSchemaCommands(connection, DatabaseSchema.AllTables);
    }

    private void ExecuteSchemaCommands(SqliteConnection connection, IEnumerable<string> commands)
    {
        foreach (string sql in commands)
        {
            using SqliteCommand command = new SqliteCommand(sql, connection);
            command.ExecuteNonQuery();
        }
    }

    private void CreateIndexes(SqliteConnection connection)
    {
        string[] indexes =
        {
            "CREATE INDEX IF NOT EXISTS idx_folders_path ON Folders(Path)",
            "CREATE INDEX IF NOT EXISTS idx_songs_folderid ON Songs(FolderId)",
            "CREATE INDEX IF NOT EXISTS idx_songs_filepath ON Songs(FilePath)",
            "CREATE INDEX IF NOT EXISTS idx_songs_artistid ON Songs(ArtistId)",
            "CREATE INDEX IF NOT EXISTS idx_songs_albumid ON Songs(AlbumId)",
            "CREATE INDEX IF NOT EXISTS idx_albums_artistid ON Albums(ArtistId)",
            "CREATE INDEX IF NOT EXISTS idx_artists_name ON Artists(Name)",
            "CREATE INDEX IF NOT EXISTS idx_albums_name_artist ON Albums(Name, ArtistId)"
        };

        ExecuteSchemaCommands(connection, indexes);
        Logger.Debug("Database indexes created/verified");
    }

    // Song Management
    public static void AddSong(string audioFile)
    {
        Logger.Info($"Adding single song to database: {audioFile}");

        if (!File.Exists(audioFile))
        {
            Logger.Warning($"File not found: {audioFile}");
            return;
        }

        DatabaseManager manager = new DatabaseManager();
        manager.AddSongs([audioFile]);
    }

    public void AddSongs(List<string> audioFiles)
    {
        Logger.Info($"Starting to add {audioFiles.Count} songs to the database");

        using SqliteConnection connection = CreateConnection();
        connection.Open();

        IndexingContext indexingContext = new IndexingContext(connection);

        using SqliteTransaction transaction = connection.BeginTransaction();

        try
        {
            ProcessAudioFiles(audioFiles, connection, transaction, indexingContext);
            transaction.Commit();
            Logger.Info($"Successfully indexed {audioFiles.Count} songs");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to add songs: {ex}");
            transaction.Rollback();
            throw;
        }
    }

    public static void DeleteSong(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            Logger.Warning("Cannot delete song: file path is null or empty");
            return;
        }

        Logger.Info($"Deleting song from database: {filePath}");

        using SqliteConnection connection = CreateConnection();
        connection.Open();

        using SqliteTransaction transaction = connection.BeginTransaction();

        try
        {
            using SqliteCommand command = new SqliteCommand(
                "DELETE FROM Songs WHERE FilePath = @FilePath", connection, transaction);
            command.Parameters.AddWithValue("@FilePath", filePath);

            int rowsAffected = command.ExecuteNonQuery();

            if (rowsAffected > 0)
            {
                Logger.Info($"Deleted song: {filePath}");
                _artworkCache.Clear();
            }
            else
            {
                Logger.Warning($"No song found with FilePath: {filePath}");
            }

            transaction.Commit();

            SongDeleted?.Invoke(filePath);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to delete song '{filePath}': {ex.Message}");
            transaction.Rollback();
            throw;
        }
    }

    public static void DeleteFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            Logger.Warning("Cannot delete folder: folder path is null or empty");
            return;
        }

        Logger.Info($"Deleting folder and all songs from database: {folderPath}");

        using SqliteConnection connection = CreateConnection();
        connection.Open();

        using SqliteTransaction transaction = connection.BeginTransaction();

        try
        {
            // First, get all song file paths in this folder
            List<string> deletedSongPaths = GetSongPathsInFolder(connection, transaction, folderPath);
            
            int songsDeleted = DeleteSongsInFolder(connection, transaction, folderPath);
            int foldersDeleted = DeleteFolderRecord(connection, transaction, folderPath);

            transaction.Commit();

            if (foldersDeleted > 0)
            {
                Logger.Info($"Deleted folder '{folderPath}' and {songsDeleted} songs");
                _artworkCache.Clear();

                // Invoke events
                FolderDeleted?.Invoke(folderPath);
                SongsDeleted?.Invoke(deletedSongPaths);
            }
            else
            {
                Logger.Warning($"No folder found with path: {folderPath}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to delete folder '{folderPath}': {ex.Message}");
            transaction.Rollback();
            throw;
        }
    }

    private void ProcessAudioFiles(List<string> audioFiles, SqliteConnection connection, SqliteTransaction transaction, IndexingContext context)
    {
        foreach (string audioFile in audioFiles)
        {
            try
            {
                ProcessSingleFile(audioFile, connection, transaction, context);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to process file '{audioFile}': {ex.Message}");
                // Continue with the next file instead of failing the entire batch
            }
        }
    }

    private void ProcessSingleFile(string audioFile, SqliteConnection connection, SqliteTransaction transaction, IndexingContext context)
    {
        Logger.Debug($"Processing file: {audioFile}");

        AudioMetadata metadata = AudioMetadata.Extract(audioFile);
        (string Path, string Name) folderInfo = GetFolderInfo(audioFile);

        long folderId = context.GetOrCreateFolder(folderInfo.Path, folderInfo.Name, transaction);
        long artistId = context.GetOrCreateArtist(metadata.Artist, transaction);
        long? albumId = GetAlbumIdIfExists(metadata, artistId, context, transaction);

        InsertSong(connection, transaction, metadata, artistId, albumId, folderId);
    }

    private long? GetAlbumIdIfExists(AudioMetadata metadata, long artistId, IndexingContext context, SqliteTransaction transaction)
    {
        if (string.IsNullOrEmpty(metadata.Album))
        {
            return null;
        }

        return context.GetOrCreateAlbum(metadata.Album, artistId, metadata.ArtworkData, transaction);
    }

    private (string Path, string Name) GetFolderInfo(string audioFile)
    {
        string folderPath = Path.GetDirectoryName(audioFile) ?? "Unknown";
        string folderName = Path.GetFileName(folderPath) ?? "Unknown";
        return (folderPath, folderName);
    }

    private void InsertSong(SqliteConnection connection, SqliteTransaction transaction, AudioMetadata metadata, long artistId, long? albumId, long folderId)
    {
        Logger.Trace($"Inserting song: {metadata.Title}");

        byte[]? artwork = ShouldStoreArtwork(albumId, metadata) ? metadata.ArtworkData : null;

        using SqliteCommand command = CreateInsertSongCommand(connection, transaction);
        SetSongParameters(command, metadata, artistId, albumId, folderId, artwork);

        int rowsAffected = command.ExecuteNonQuery();
        LogInsertResult(rowsAffected, metadata);
    }

    private bool ShouldStoreArtwork(long? albumId, AudioMetadata metadata)
    {
        return albumId == null && metadata.ArtworkData != null;
    }

    private SqliteCommand CreateInsertSongCommand(SqliteConnection connection, SqliteTransaction transaction)
    {
        const string sql = @"
            INSERT OR IGNORE INTO Songs
            (Title, ArtistId, AlbumId, Artwork, Duration, FileSize, FilePath, FolderId)
            VALUES
            (@Title, @ArtistId, @AlbumId, @Artwork, @Duration, @FileSize, @FilePath, @FolderId)";

        return new SqliteCommand(sql, connection, transaction);
    }

    private void SetSongParameters(SqliteCommand command, AudioMetadata metadata, long artistId, long? albumId, long folderId, byte[]? artwork)
    {
        command.Parameters.AddWithValue("@Title", metadata.Title);
        command.Parameters.AddWithValue("@ArtistId", artistId);
        command.Parameters.AddWithValue("@AlbumId", (object?)albumId ?? DBNull.Value);
        command.Parameters.AddWithValue("@Artwork", (object?)artwork ?? DBNull.Value);
        command.Parameters.AddWithValue("@Duration", (object?)metadata.Duration ?? DBNull.Value);
        command.Parameters.AddWithValue("@FileSize", new FileInfo(metadata.FilePath!).Length);
        command.Parameters.AddWithValue("@FilePath", metadata.FilePath ?? string.Empty);
        command.Parameters.AddWithValue("@FolderId", folderId);
    }

    private void LogInsertResult(int rowsAffected, AudioMetadata metadata)
    {
        if (rowsAffected == 0)
        {
            Logger.Trace($"Skipped duplicate song: {metadata.FilePath}");
        }
        else
        {
            Logger.Debug($"Inserted song: {metadata.Title} ({metadata.FilePath})");
        }
    }

    // Querying
    public List<FolderRecord> GetAllFolders()
    {
        Logger.Info("Fetching all folders from the database...");

        const string query = @"
            SELECT f.Id, f.Name, f.Path, COUNT(s.Id) as SongCount
            FROM Folders f
            LEFT JOIN Songs s ON f.Id = s.FolderId
            GROUP BY f.Id, f.Name, f.Path
            ORDER BY f.Name";

        return ExecuteQuery(query, reader => new FolderRecord
        {
            Id = reader.GetInt64(0),
            Name = reader.GetString(1),
            Path = reader.GetString(2),
            SongCount = reader.GetInt32(3)
        });
    }

    public List<AudioMetadata> GetSongsFolder(string folderPath)
    {
        Logger.Info($"Fetching songs from folder: {folderPath}");

        const string query = @"
            SELECT s.Title, a.Name AS Artist, al.Name as Album, s.Duration, s.FilePath
            FROM Songs s
            LEFT JOIN Artists a ON s.ArtistId = a.Id
            LEFT JOIN Albums al ON s.AlbumId = al.Id
            LEFT JOIN Folders f ON s.FolderId = f.Id
            WHERE f.Path = @FolderPath
            ORDER BY s.Title";

        return ExecuteQuery(query, reader => ReadAudioMetadata(reader),
            cmd => cmd.Parameters.AddWithValue("@FolderPath", folderPath));
    }

    public static AudioMetadata? GetSongByFilePath(string filePath)
    {
        Logger.Info($"Fetching song from database: {filePath}");

        const string query = @"
            SELECT s.Title, a.Name AS Artist, al.Name AS Album, s.Duration, s.FilePath
            FROM Songs s
            LEFT JOIN Artists a ON s.ArtistId = a.Id
            LEFT JOIN Albums al ON s.AlbumId = al.Id
            WHERE s.FilePath = @FilePath
            LIMIT 1";

        List<AudioMetadata> results = ExecuteStaticQuery(query, reader => ReadAudioMetadata(reader), cmd => cmd.Parameters.AddWithValue("@FilePath", filePath));

        AudioMetadata? song = results.FirstOrDefault();

        if (song == null)
        {
            Logger.Warning($"No song found with file path: {filePath}");
        }
        else
        {
            Logger.Info($"Loaded song '{song.Title}' by '{song.Artist}'");
        }

        return song;
    }

    public static byte[]? GetSongArtwork(string filePath)
    {
        return _artworkCache.GetOrAdd(filePath, () =>
        {
            const string query = @"
                SELECT COALESCE(s.Artwork, al.Artwork) AS Artwork
                FROM Songs s
                LEFT JOIN Albums al ON s.AlbumId = al.Id
                WHERE s.FilePath = @FilePath";

            using SqliteConnection connection = CreateConnection();
            connection.Open();

            using SqliteCommand command = new SqliteCommand(query, connection);
            command.Parameters.AddWithValue("@FilePath", filePath);

            try
            {
                return command.ExecuteScalar() as byte[];
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading artwork for '{filePath}': {ex.Message}");
                return null;
            }
        });
    }

    public static void ClearArtworkCache() => _artworkCache.Clear();

    // Helpers
    private static SqliteConnection CreateConnection() => new SqliteConnection(ConnectionString);

    private static AudioMetadata ReadAudioMetadata(SqliteDataReader reader)
    {
        return new AudioMetadata
        {
            Title = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
            Artist = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            Album = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            Duration = reader.IsDBNull(3) ? null : reader.GetDouble(3),
            FilePath = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            ArtworkData = null // Artwork loaded on-demand
        };
    }

    private List<T> ExecuteQuery<T>(string query, Func<SqliteDataReader, T> mapper, Action<SqliteCommand>? parameterSetter = null)
    {
        return ExecuteStaticQuery(query, mapper, parameterSetter);
    }

    private static List<T> ExecuteStaticQuery<T>(string query, Func<SqliteDataReader, T> mapper, Action<SqliteCommand>? parameterSetter = null)
    {
        List<T> results = new List<T>();

        using SqliteConnection connection = CreateConnection();
        connection.Open();

        using SqliteCommand command = new SqliteCommand(query, connection);
        parameterSetter?.Invoke(command);

        try
        {
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(mapper(reader));
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Query execution failed: {ex.Message}");
        }

        return results;
    }

    private static List<string> GetSongPathsInFolder(SqliteConnection connection, SqliteTransaction transaction, string folderPath)
    {
        List<string> songPaths = new List<string>();

        const string query = @"
        SELECT s.FilePath
        FROM Songs s
        INNER JOIN Folders f ON s.FolderId = f.Id
        WHERE f.Path = @FolderPath";

        using SqliteCommand command = new SqliteCommand(query, connection, transaction);
        command.Parameters.AddWithValue("@FolderPath", folderPath);

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string? filePath = reader.IsDBNull(0) ? null : reader.GetString(0);
            if (!string.IsNullOrEmpty(filePath))
            {
                songPaths.Add(filePath);
            }
        }

        return songPaths;
    }

    private static int DeleteSongsInFolder(SqliteConnection connection, SqliteTransaction transaction, string folderPath)
    {
        const string deleteSongsQuery = @"
        DELETE FROM Songs
        WHERE FolderId IN (
            SELECT Id FROM Folders WHERE Path = @FolderPath
        )";

        using SqliteCommand command = new SqliteCommand(deleteSongsQuery, connection, transaction);
        command.Parameters.AddWithValue("@FolderPath", folderPath);

        return command.ExecuteNonQuery();
    }

    private static int DeleteFolderRecord(SqliteConnection connection, SqliteTransaction transaction, string folderPath)
    {
        const string deleteFolderQuery = "DELETE FROM Folders WHERE Path = @FolderPath";

        using SqliteCommand command = new SqliteCommand(deleteFolderQuery, connection, transaction);
        command.Parameters.AddWithValue("@FolderPath", folderPath);

        return command.ExecuteNonQuery();
    }

    // Indexing Context Class
    private class IndexingContext
    {
        private readonly SqliteConnection _connection;
        private readonly Dictionary<string, long> _folderCache;
        private readonly Dictionary<string, long> _artistCache;
        private readonly Dictionary<(string, long), long> _albumCache;

        public IndexingContext(SqliteConnection connection)
        {
            _connection = connection;
            _folderCache = LoadFolderCache();
            _artistCache = LoadArtistCache();
            _albumCache = LoadAlbumCache();
        }

        public long GetOrCreateFolder(string path, string name, SqliteTransaction transaction)
        {
            if (_folderCache.TryGetValue(path, out var id))
            {
                return id;
            }

            Logger.Info($"Creating new folder entry: {name}");
            id = ExecuteInsert(
                "INSERT INTO Folders (Name, Path) VALUES (@Name, @Path)",
                transaction,
                cmd =>
                {
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.Parameters.AddWithValue("@Path", path);
                });

            _folderCache[path] = id;
            return id;
        }

        public long GetOrCreateArtist(string name, SqliteTransaction transaction)
        {
            if (_artistCache.TryGetValue(name, out var id))
            {
                return id;
            }

            Logger.Info($"Creating new artist entry: {name}");
            id = ExecuteInsert(
                "INSERT INTO Artists (Name) VALUES (@Name)",
                transaction,
                cmd => cmd.Parameters.AddWithValue("@Name", name));

            _artistCache[name] = id;
            return id;
        }

        public long GetOrCreateAlbum(string name, long artistId, byte[]? artwork, SqliteTransaction transaction)
        {
            (string name, long artistId) key = (name, artistId);
            if (_albumCache.TryGetValue(key, out var id))
            {
                return id;
            }

            id = ExecuteInsert(
                "INSERT INTO Albums (Name, ArtistId, Year, Artwork) VALUES (@Name, @ArtistId, @Year, @Artwork)",
                transaction,
                cmd =>
                {
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.Parameters.AddWithValue("@ArtistId", artistId);
                    cmd.Parameters.AddWithValue("@Year", DBNull.Value);
                    cmd.Parameters.AddWithValue("@Artwork", (object?)artwork ?? DBNull.Value);
                });

            _albumCache[key] = id;
            return id;
        }

        private long ExecuteInsert(string sql, SqliteTransaction transaction, Action<SqliteCommand> parameterSetter)
        {
            using SqliteCommand command = new SqliteCommand($"{sql}; SELECT last_insert_rowid();", _connection, transaction);
            parameterSetter(command);
            return (long)command.ExecuteScalar()!;
        }

        private Dictionary<string, long> LoadFolderCache()
        {
            return LoadCache("SELECT Path, Id FROM Folders", reader => (reader.GetString(0), reader.GetInt64(1)));
        }

        private Dictionary<string, long> LoadArtistCache()
        {
            return LoadCache("SELECT Name, Id FROM Artists", reader => (reader.GetString(0), reader.GetInt64(1)));
        }

        private Dictionary<(string, long), long> LoadAlbumCache()
        {
            Dictionary<(string, long), long> cache = new Dictionary<(string, long), long>();
            using SqliteCommand cmd = new SqliteCommand("SELECT Name, ArtistId, Id FROM Albums", _connection);
            using SqliteDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                cache[(reader.GetString(0), reader.GetInt64(1))] = reader.GetInt64(2);
            }

            Logger.Debug($"Loaded {cache.Count} albums into cache");
            return cache;
        }

        private Dictionary<string, long> LoadCache(string query, Func<SqliteDataReader, (string key, long value)> mapper)
        {
            Dictionary<string, long> cache = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            using SqliteCommand cmd = new SqliteCommand(query, _connection);
            using SqliteDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                (string key, long value) = mapper(reader);
                cache[key] = value;
            }

            Logger.Debug($"Loaded {cache.Count} items into cache");
            return cache;
        }
    }

    private class ArtworkCache
    {
        private readonly Dictionary<string, byte[]?> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        public byte[]? GetOrAdd(string key, Func<byte[]?> factory)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var cached))
                {
                    return cached;
                }

                byte[]? value = factory();
                _cache[key] = value;

                if (_cache.Count > MAX_ARTWORK_CACHE_SIZE)
                {
                    TrimCache();
                }

                return value;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _cache.Clear();
                Logger.Info("Artwork cache cleared");
            }
        }

        private void TrimCache()
        {
            List<string> toRemove = _cache.Keys.Take(50).ToList();
            foreach (string key in toRemove)
            {
                _cache.Remove(key);
            }
        }
    }
}