namespace FluentAurora.Core.Indexer;

public static class DatabaseSchema
{
    // Artists table
    private const string CREATE_ARTISTS_TABLE = @"
        CREATE TABLE IF NOT EXISTS Artists (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL UNIQUE
        );";

    // Albums table
    private const string CREATE_ALBUMS_TABLE = @"
        CREATE TABLE IF NOT EXISTS Albums (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            ArtistId INTEGER,
            Year INTEGER,
            Artwork BLOB,
            UNIQUE(Name, ArtistId),
            FOREIGN KEY (ArtistId) REFERENCES Artists(Id)
        );";
    
    // Genres Table
    private const string CREATE_FOLDERS_TABLE = @"
        CREATE TABLE IF NOT EXISTS Folders (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Path TEXT NOT NULL UNIQUE
        );";

    // Songs table
    private const string CREATE_SONGS_TABLE = @"
        CREATE TABLE IF NOT EXISTS Songs (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Title TEXT,
            ArtistId INTEGER,
            AlbumId INTEGER,
            Artwork BLOB,
            Duration INTEGER,
            FileSize INTEGER,
            FilePath TEXT NOT NULL UNIQUE,
            FolderId INTEGER,
            FOREIGN KEY (ArtistId) REFERENCES Artists(Id),
            FOREIGN KEY (AlbumId) REFERENCES Albums(Id),
            FOREIGN KEY (FolderId) REFERENCES Folders(Id)
        );";

    // Array of all tables to execute during initialization
    public static readonly string[] AllTables =
    {
        CREATE_ARTISTS_TABLE,
        CREATE_ALBUMS_TABLE,
        CREATE_FOLDERS_TABLE,
        CREATE_SONGS_TABLE
    };
}