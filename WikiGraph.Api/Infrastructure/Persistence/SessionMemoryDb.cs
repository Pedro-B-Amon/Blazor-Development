namespace WikiGraph.Api.Infrastructure.Persistence;

public sealed class SessionMemoryDb
{
    public const string DefaultUserId = "local-device";

    private readonly ISqliteConnectionFactory _connectionFactory;

    public SessionMemoryDb(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
        InitializeSchema();
    }

    private void InitializeSchema()
    {
        using var connection = _connectionFactory.OpenConnection();

        connection.ExecuteNonQuery("""
            CREATE TABLE IF NOT EXISTS Users (
                UserId TEXT PRIMARY KEY,
                CreatedUtc TEXT NOT NULL
            );
            """);

        connection.ExecuteNonQuery("""
            CREATE TABLE IF NOT EXISTS Sessions (
                SessionId TEXT PRIMARY KEY,
                UserId TEXT NOT NULL,
                Title TEXT NOT NULL,
                CreatedUtc TEXT NOT NULL,
                LastAccessUtc TEXT NOT NULL,
                FOREIGN KEY(UserId) REFERENCES Users(UserId) ON DELETE CASCADE
            );
            """);

        connection.ExecuteNonQuery("""
            CREATE INDEX IF NOT EXISTS IX_Sessions_UserId_LastAccessUtc
            ON Sessions (UserId, LastAccessUtc DESC);
            """);

        connection.ExecuteNonQuery("""
            CREATE TABLE IF NOT EXISTS Messages (
                MessageId INTEGER PRIMARY KEY AUTOINCREMENT,
                SessionId TEXT NOT NULL,
                Role TEXT NOT NULL,
                Content TEXT NOT NULL,
                CreatedUtc TEXT NOT NULL,
                FOREIGN KEY(SessionId) REFERENCES Sessions(SessionId) ON DELETE CASCADE
            );
            """);

        connection.ExecuteNonQuery("""
            CREATE TABLE IF NOT EXISTS Citations (
                CitationId INTEGER PRIMARY KEY AUTOINCREMENT,
                SessionId TEXT NOT NULL,
                Title TEXT NOT NULL,
                Url TEXT NOT NULL,
                Section TEXT NULL,
                FOREIGN KEY(SessionId) REFERENCES Sessions(SessionId) ON DELETE CASCADE
            );
            """);

        connection.ExecuteNonQuery("""
            CREATE TABLE IF NOT EXISTS Graphs (
                GraphId INTEGER PRIMARY KEY AUTOINCREMENT,
                SessionId TEXT NOT NULL,
                Topic TEXT NOT NULL,
                NodesJson TEXT NOT NULL,
                EdgesJson TEXT NOT NULL,
                FOREIGN KEY(SessionId) REFERENCES Sessions(SessionId) ON DELETE CASCADE
            );
            """);

        connection.ExecuteNonQuery("""
            CREATE TABLE IF NOT EXISTS WikipediaPages (
                PageId TEXT PRIMARY KEY,
                SessionId TEXT NOT NULL,
                Title TEXT NOT NULL,
                SourceUrl TEXT NOT NULL,
                RetrievedUtc TEXT NOT NULL,
                FOREIGN KEY(SessionId) REFERENCES Sessions(SessionId) ON DELETE CASCADE
            );
            """);

        connection.ExecuteNonQuery("""
            CREATE TABLE IF NOT EXISTS DocumentChunks (
                ChunkId TEXT PRIMARY KEY,
                PageId TEXT NOT NULL,
                Section TEXT NOT NULL,
                Text TEXT NOT NULL,
                Hash TEXT NOT NULL,
                FOREIGN KEY(PageId) REFERENCES WikipediaPages(PageId) ON DELETE CASCADE
            );
            """);

        connection.ExecuteNonQuery("""
            CREATE TABLE IF NOT EXISTS ChunkEmbeddings (
                ChunkId TEXT PRIMARY KEY,
                Embedding BLOB NOT NULL,
                FOREIGN KEY(ChunkId) REFERENCES DocumentChunks(ChunkId) ON DELETE CASCADE
            );
            """);

        using var seedCommand = connection.CreateCommand();
        seedCommand.CommandText = """
            INSERT OR IGNORE INTO Users (UserId, CreatedUtc)
            VALUES ($userId, $createdUtc);
            """;
        seedCommand.Parameters.AddWithValue("$userId", DefaultUserId);
        seedCommand.Parameters.AddWithValue("$createdUtc", DateTime.UtcNow.ToString("O"));
        seedCommand.ExecuteNonQuery();
    }
}
