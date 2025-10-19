using FluentAurora.Core.Logging;
using FluentAurora.Core.Paths;
using FluentAurora.Core.Playback;
using Microsoft.Data.Sqlite;

namespace FluentAurora.Core.Indexer;

public class DatabaseManager
{
    // Variables
    public string ConnectionString => $"Data Source={PathResolver.Database}";

    // Constructors
    public DatabaseManager()
    {
        EnsureDatabase();
    }

    // Methods
    private void EnsureDatabase()
    {
        Logger.Info("Checking if the library database exists");
        bool dbExists = File.Exists(PathResolver.Database);
        using SqliteConnection connection = new SqliteConnection(ConnectionString);
        connection.Open();

        if (!dbExists)
        {
            Logger.Warning("The library database does not exist — creating a new one");
            CreateDatabase(connection);
        }
        else
        {
            Logger.Debug("Verifying all tables exist in the existing database");
            foreach (string tableSql in DatabaseSchema.AllTables)
            {
                using SqliteCommand command = new SqliteCommand(tableSql, connection);
                command.ExecuteNonQuery();
            }
        }
    }

    private void CreateDatabase(SqliteConnection connection)
    {
        Logger.Info("Creating library database schema...");
        foreach (string tableSql in DatabaseSchema.AllTables)
        {
            using SqliteCommand command = new SqliteCommand(tableSql, connection);
            command.ExecuteNonQuery();
        }
        Logger.Info("Database schema created successfully");
    }

    public void AddSongs(List<string> audioFiles)
    {
        Logger.Info($"Starting to add {audioFiles.Count} songs to the database");
        using SqliteConnection connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using SqliteTransaction transaction = connection.BeginTransaction();
        try
        {
            foreach (string audioFile in audioFiles)
            {
                Logger.Debug($"Processing file: {audioFile}");
                AudioMetadata metadata = AudioMetadata.Extract(audioFile);

                string? folderPath = Path.GetDirectoryName(audioFile);
                string folderName = Path.GetFileName(folderPath ?? "Unknown");

                long folderId = GetOrCreateFolderId(connection, transaction, folderPath!, folderName);
                long artistId = GetOrCreateArtistId(connection, transaction, metadata.Artist);

                long? albumId = null;
                if (!string.IsNullOrEmpty(metadata.Album))
                {
                    albumId = GetOrCreateAlbumId(connection, transaction, metadata.Album, artistId, metadata.ArtworkData);
                }

                InsertSong(connection, transaction, metadata, artistId, albumId, folderId);
            }

            transaction.Commit();
            Logger.Info($"Successfully indexed {audioFiles.Count} songs");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to add songs: {ex}");
            transaction.Rollback();
        }
    }

    private long GetOrCreateFolderId(SqliteConnection connection, SqliteTransaction transaction, string path, string name)
    {
        Logger.Trace($"Looking up folder: {path}");
        using SqliteCommand selectCommand = new SqliteCommand("SELECT Id FROM Folders WHERE Path = @Path", connection, transaction);
        selectCommand.Parameters.AddWithValue("@Path", path);
        object? result = selectCommand.ExecuteScalar();
        if (result != null)
        {
            Logger.Debug($"Found existing folder: {name} (ID: {result})");
            return (long)result;
        }

        Logger.Info($"Creating new folder entry: {name}");
        using SqliteCommand insertCommand = new SqliteCommand(
            "INSERT INTO Folders (Name, Path) VALUES (@Name, @Path); SELECT last_insert_rowid();",
            connection,
            transaction);
        insertCommand.Parameters.AddWithValue("@Name", name);
        insertCommand.Parameters.AddWithValue("@Path", path);
        long id = (long)insertCommand.ExecuteScalar()!;
        Logger.Debug($"Created folder '{name}' with ID {id}");
        return id;
    }

    private long GetOrCreateArtistId(SqliteConnection connection, SqliteTransaction transaction, string artistName)
    {
        Logger.Trace($"Looking up artist: {artistName}");
        using SqliteCommand selectCommand = new SqliteCommand("SELECT Id FROM Artists WHERE Name = @Name", connection, transaction);
        selectCommand.Parameters.AddWithValue("@Name", artistName);
        object? result = selectCommand.ExecuteScalar();
        if (result != null)
        {
            Logger.Debug($"Found existing artist: {artistName} (ID: {result})");
            return (long)result;
        }

        Logger.Info($"Creating new artist entry: {artistName}");
        using SqliteCommand insertCommand = new SqliteCommand("INSERT INTO Artists (Name) VALUES (@Name); SELECT last_insert_rowid();", connection, transaction);
        insertCommand.Parameters.AddWithValue("@Name", artistName);
        long id = (long)insertCommand.ExecuteScalar()!;
        Logger.Debug($"Created artist '{artistName}' with ID {id}");
        return id;
    }

    private long GetOrCreateAlbumId(SqliteConnection connection, SqliteTransaction transaction, string album, long artistId, byte[]? artwork)
    {
        using SqliteCommand selectCommand = new SqliteCommand("SELECT Id FROM Albums WHERE Name = @Name AND ArtistId = @ArtistId", connection, transaction);
        selectCommand.Parameters.AddWithValue("@Name", album);
        selectCommand.Parameters.AddWithValue("@ArtistId", artistId);
        object? result = selectCommand.ExecuteScalar();
        if (result != null)
            return (long)result;

        using SqliteCommand insertCommand = new SqliteCommand(
            "INSERT INTO Albums (Name, ArtistId, Year, Artwork) VALUES (@Name, @ArtistId, @Year, @Artwork); SELECT last_insert_rowid();",
            connection,
            transaction);

        insertCommand.Parameters.AddWithValue("@Name", album);
        insertCommand.Parameters.AddWithValue("@ArtistId", artistId);
        insertCommand.Parameters.AddWithValue("@Year", DBNull.Value);
        insertCommand.Parameters.AddWithValue("@Artwork", (object?)artwork ?? DBNull.Value);

        return (long)insertCommand.ExecuteScalar()!;
    }

    private void InsertSong(SqliteConnection connection, SqliteTransaction transaction, AudioMetadata metadata, long? artistId, long? albumId, long folderId)
    {
        try
        {
            Logger.Trace($"Inserting song: {metadata.Title}");
            byte[]? artworkToStore = (albumId == null && metadata.ArtworkData != null) ? metadata.ArtworkData : null;

            using SqliteCommand insertCommand = new SqliteCommand(
                @"INSERT OR IGNORE INTO Songs
                  (Title, ArtistId, AlbumId, Artwork, Duration, FileSize, FilePath, FolderId)
                  VALUES
                  (@Title, @ArtistId, @AlbumId, @Artwork, @Duration, @FileSize, @FilePath, @FolderId);",
                connection,
                transaction);

            insertCommand.Parameters.AddWithValue("@Title", metadata.Title);
            insertCommand.Parameters.AddWithValue("@ArtistId", artistId);
            insertCommand.Parameters.AddWithValue("@AlbumId", (object?)albumId ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("@Artwork", (object?)artworkToStore ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("@Duration", (object?)metadata.Duration ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("@FileSize", new FileInfo(metadata.FilePath!).Length);
            insertCommand.Parameters.AddWithValue("@FilePath", metadata.FilePath ?? string.Empty);
            insertCommand.Parameters.AddWithValue("@FolderId", folderId);

            int rows = insertCommand.ExecuteNonQuery();
            if (rows == 0)
                Logger.Trace($"Skipped duplicate song: {metadata.FilePath}");
            else
                Logger.Debug($"Inserted song: {metadata.Title} ({metadata.FilePath})");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to insert song '{metadata.FilePath}': {ex.Message}");
        }
    }

    public List<FolderRecord> GetAllFolders()
    {
        Logger.Info("Fetching all folders from the database...");
        List<FolderRecord> folders = [];

        using SqliteConnection connection = new SqliteConnection(ConnectionString);
        connection.Open();

        string query = "SELECT Id, Name, Path FROM Folders ORDER BY Name;";
        using SqliteCommand command = new SqliteCommand(query, connection);

        try
        {
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                folders.Add(new FolderRecord
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    Path = reader.GetString(2)
                });
            }

            Logger.Info($"Loaded {folders.Count} folders from the database");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error loading folders: {ex.Message}");
        }

        return folders;
    }


    public List<AudioMetadata> GetSongsByFolder(string folderPath)
    {
        Logger.Info($"Fetching songs from folder: {folderPath}");
        List<AudioMetadata> songs = [];

        using SqliteConnection connection = new SqliteConnection(ConnectionString);
        connection.Open();

        string query = @"SELECT s.Title, a.Name AS Artist, al.Name as Album, COALESCE(al.Artwork, s.Artwork) AS Artwork, s.Duration, s.FilePath
FROM Songs s
LEFT JOIN Artists a ON s.ArtistId = a.Id
LEFT JOIN Albums al ON s.AlbumId = al.Id
LEFT JOIN Folders f ON s.FolderId = f.Id
WHERE f.Path = @FolderPath
ORDER BY s.Title";

        using SqliteCommand command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@FolderPath", folderPath);

        try
        {
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                AudioMetadata metadata = new AudioMetadata
                {
                    Title = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    Artist = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Album = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Duration = reader.IsDBNull(4) ? null : reader.GetDouble(4),
                    FilePath = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    ArtworkData = reader.IsDBNull(3) ? null : (byte[])reader[3]
                };

                songs.Add(metadata);
            }

            Logger.Info($"Loaded {songs.Count} songs from folder '{folderPath}'");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error loading songs for folder '{folderPath}': {ex.Message}");
        }

        return songs;
    }
}