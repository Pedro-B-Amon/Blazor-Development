using Microsoft.Data.Sqlite;

namespace WikiGraph.Api.Infrastructure.Persistence;

/// <summary>
/// Owns the lightweight SQLite schema used for sessions, messages, citations, graphs, and page chunks.
/// </summary>
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

        // Keep schema creation in one ordered list so new tables are easy to add without scattering setup calls.
        string[] schemaStatements =
        [
            """
            CREATE TABLE IF NOT EXISTS Users (
                UserId TEXT PRIMARY KEY,
                CreatedUtc TEXT NOT NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS Sessions (
                SessionId TEXT PRIMARY KEY,
                UserId TEXT NOT NULL,
                Title TEXT NOT NULL,
                CreatedUtc TEXT NOT NULL,
                LastAccessUtc TEXT NOT NULL,
                FOREIGN KEY(UserId) REFERENCES Users(UserId) ON DELETE CASCADE
            );
            """,
            """
            CREATE INDEX IF NOT EXISTS IX_Sessions_UserId_LastAccessUtc
            ON Sessions (UserId, LastAccessUtc DESC);
            """,
            """
            CREATE TABLE IF NOT EXISTS Messages (
                MessageId INTEGER PRIMARY KEY AUTOINCREMENT,
                SessionId TEXT NOT NULL,
                Role TEXT NOT NULL,
                Content TEXT NOT NULL,
                CreatedUtc TEXT NOT NULL,
                FOREIGN KEY(SessionId) REFERENCES Sessions(SessionId) ON DELETE CASCADE
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS Citations (
                CitationId INTEGER PRIMARY KEY AUTOINCREMENT,
                SessionId TEXT NOT NULL,
                MessageId INTEGER NULL,
                Title TEXT NOT NULL,
                Url TEXT NOT NULL,
                Section TEXT NULL,
                ChunkId TEXT NULL,
                FOREIGN KEY(SessionId) REFERENCES Sessions(SessionId) ON DELETE CASCADE,
                FOREIGN KEY(MessageId) REFERENCES Messages(MessageId) ON DELETE CASCADE
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS Graphs (
                GraphId INTEGER PRIMARY KEY AUTOINCREMENT,
                SessionId TEXT NOT NULL,
                Topic TEXT NOT NULL,
                NodesJson TEXT NOT NULL,
                EdgesJson TEXT NOT NULL,
                FOREIGN KEY(SessionId) REFERENCES Sessions(SessionId) ON DELETE CASCADE
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS WikipediaPages (
                PageId TEXT PRIMARY KEY,
                SessionId TEXT NOT NULL,
                Title TEXT NOT NULL,
                SourceUrl TEXT NOT NULL,
                RetrievedUtc TEXT NOT NULL,
                FOREIGN KEY(SessionId) REFERENCES Sessions(SessionId) ON DELETE CASCADE
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS DocumentChunks (
                ChunkId TEXT PRIMARY KEY,
                PageId TEXT NOT NULL,
                Section TEXT NOT NULL,
                Text TEXT NOT NULL,
                Hash TEXT NOT NULL,
                FOREIGN KEY(PageId) REFERENCES WikipediaPages(PageId) ON DELETE CASCADE
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS ChunkEmbeddings (
                ChunkId TEXT PRIMARY KEY,
                Embedding BLOB NOT NULL,
                EncodingKind TEXT NOT NULL DEFAULT 'keywords',
                ModelId TEXT NULL,
                Dimensions INTEGER NULL,
                FOREIGN KEY(ChunkId) REFERENCES DocumentChunks(ChunkId) ON DELETE CASCADE
            );
            """
        ];

        foreach (var statement in schemaStatements)
        {
            connection.ExecuteNonQuery(statement);
        }

        EnsureColumn(connection, "Citations", "MessageId", "INTEGER NULL");
        EnsureColumn(connection, "Citations", "ChunkId", "TEXT NULL");
        EnsureColumn(connection, "ChunkEmbeddings", "EncodingKind", "TEXT NOT NULL DEFAULT 'keywords'");
        EnsureColumn(connection, "ChunkEmbeddings", "ModelId", "TEXT NULL");
        EnsureColumn(connection, "ChunkEmbeddings", "Dimensions", "INTEGER NULL");

        // Seed the default local user once so session rows always have a valid parent user.
        using var seedCommand = connection.CreateCommand();
        seedCommand.CommandText = """
            INSERT OR IGNORE INTO Users (UserId, CreatedUtc)
            VALUES ($userId, $createdUtc);
            """;
        seedCommand.Parameters.AddWithValue("$userId", DefaultUserId);
        seedCommand.Parameters.AddWithValue("$createdUtc", DateTime.UtcNow.ToString("O"));
        seedCommand.ExecuteNonQuery();
    }

    private static void EnsureColumn(SqliteConnection connection, string tableName, string columnName, string columnDefinition)
    {
        // Treat schema drift as a lightweight migration: only add the column when it is missing.
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        connection.ExecuteNonQuery($"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};");
    }
}
