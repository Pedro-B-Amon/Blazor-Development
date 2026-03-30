using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using WikiGraph.Contracts;

namespace WikiGraph.Api;

public interface ISessionStore
{
    SessionSummary CreateSession(string title);
    IReadOnlyList<SessionSummary> GetSessions();
    SessionDetailDto? GetSession(string sessionId);
    QueryResponse AppendQuery(QueryRequest request);
}

public interface ISqliteConnectionFactory
{
    SqliteConnection OpenConnection();
}

public sealed class SqliteConnectionFactory : ISqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("WikiGraph") ?? "Data Source=wikigraph.db";
    }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }
}

public sealed class SqliteSessionStore : ISessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ISqliteConnectionFactory _connectionFactory;

    public SqliteSessionStore(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
        // SQLite is the persistence boundary for sessions, messages, citations, and graphs.
        InitializeSchema();
    }

    public SessionSummary CreateSession(string title)
    {
        var now = DateTime.UtcNow;
        var session = new SessionSummary(Guid.NewGuid().ToString("N"), title, now, now);

        using var connection = _connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Sessions (SessionId, Title, CreatedUtc, LastAccessUtc)
            VALUES ($sessionId, $title, $createdUtc, $lastAccessUtc);
            """;
        command.Parameters.AddWithValue("$sessionId", session.SessionId);
        command.Parameters.AddWithValue("$title", session.Title);
        command.Parameters.AddWithValue("$createdUtc", session.CreatedUtc.ToString("O"));
        command.Parameters.AddWithValue("$lastAccessUtc", session.LastAccessUtc.ToString("O"));
        command.ExecuteNonQuery();

        return session;
    }

    public IReadOnlyList<SessionSummary> GetSessions()
    {
        using var connection = _connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT SessionId, Title, CreatedUtc, LastAccessUtc
            FROM Sessions
            ORDER BY LastAccessUtc DESC;
            """;

        using var reader = command.ExecuteReader();
        var sessions = new List<SessionSummary>();
        while (reader.Read())
        {
            sessions.Add(ReadSessionSummary(reader));
        }

        return sessions;
    }

    public SessionDetailDto? GetSession(string sessionId)
    {
        using var connection = _connectionFactory.OpenConnection();
        if (LoadSession(connection, sessionId) is not { } session)
        {
            return null;
        }

        return new SessionDetailDto(
            session.summary,
            LoadMessages(connection, sessionId),
            LoadCitations(connection, sessionId),
            LoadGraphs(connection, sessionId));
    }

    public QueryResponse AppendQuery(QueryRequest request)
    {
        var nowUtc = DateTime.UtcNow;
        // One query persists the user turn, assistant turn, citations, and graph payload together.
        var assistantText = BuildStudyGuide(request.Prompt, request.SourceUrl);
        var citations = BuildCitations(request.Prompt, request.SourceUrl);
        var graphs = BuildGraphs(request.Prompt);

        using var connection = _connectionFactory.OpenConnection();
        using var transaction = connection.BeginTransaction();

        var existingSession = LoadSession(connection, request.SessionId);
        if (existingSession is null)
        {
            InsertSession(connection, new SessionSummary(request.SessionId, InferTitle(request.Prompt), nowUtc, nowUtc), transaction);
        }
        else
        {
            UpdateSession(connection, request.SessionId, InferTitle(request.Prompt), nowUtc, transaction);
        }

        InsertMessage(connection, request.SessionId, new MessageDto("user", request.Prompt, nowUtc), transaction);
        InsertMessage(connection, request.SessionId, new MessageDto("assistant", assistantText, nowUtc), transaction);

        DeleteCitations(connection, request.SessionId, transaction);
        foreach (var citation in citations)
        {
            InsertCitation(connection, request.SessionId, citation, transaction);
        }

        DeleteGraphs(connection, request.SessionId, transaction);
        foreach (var graph in graphs)
        {
            InsertGraph(connection, request.SessionId, graph, transaction);
        }

        transaction.Commit();
        return new QueryResponse(request.SessionId, assistantText, citations, graphs);
    }

    private void InitializeSchema()
    {
        using var connection = _connectionFactory.OpenConnection();
        connection.ExecuteNonQuery("PRAGMA foreign_keys = ON;");
        // Schema tables map directly to session, message, citation, and graph records.
        connection.ExecuteNonQuery("""
            CREATE TABLE IF NOT EXISTS Sessions (
                SessionId TEXT PRIMARY KEY,
                Title TEXT NOT NULL,
                CreatedUtc TEXT NOT NULL,
                LastAccessUtc TEXT NOT NULL
            );
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
    }

    private static void InsertSession(SqliteConnection connection, SessionSummary session, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Sessions (SessionId, Title, CreatedUtc, LastAccessUtc)
            VALUES ($sessionId, $title, $createdUtc, $lastAccessUtc);
            """;
        command.Parameters.AddWithValue("$sessionId", session.SessionId);
        command.Parameters.AddWithValue("$title", session.Title);
        command.Parameters.AddWithValue("$createdUtc", session.CreatedUtc.ToString("O"));
        command.Parameters.AddWithValue("$lastAccessUtc", session.LastAccessUtc.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static void UpdateSession(SqliteConnection connection, string sessionId, string title, DateTime lastAccessUtc, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE Sessions
            SET Title = $title, LastAccessUtc = $lastAccessUtc
            WHERE SessionId = $sessionId;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$lastAccessUtc", lastAccessUtc.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static void InsertMessage(SqliteConnection connection, string sessionId, MessageDto message, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Messages (SessionId, Role, Content, CreatedUtc)
            VALUES ($sessionId, $role, $content, $createdUtc);
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.Parameters.AddWithValue("$role", message.Role);
        command.Parameters.AddWithValue("$content", message.Content);
        command.Parameters.AddWithValue("$createdUtc", message.CreatedUtc.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static void InsertCitation(SqliteConnection connection, string sessionId, CitationDto citation, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Citations (SessionId, Title, Url, Section)
            VALUES ($sessionId, $title, $url, $section);
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.Parameters.AddWithValue("$title", citation.Title);
        command.Parameters.AddWithValue("$url", citation.Url);
        command.Parameters.AddWithValue("$section", (object?)citation.Section ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    private static void InsertGraph(SqliteConnection connection, string sessionId, GraphDto graph, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Graphs (SessionId, Topic, NodesJson, EdgesJson)
            VALUES ($sessionId, $topic, $nodesJson, $edgesJson);
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.Parameters.AddWithValue("$topic", graph.Topic);
        command.Parameters.AddWithValue("$nodesJson", JsonSerializer.Serialize(graph.Nodes, JsonOptions));
        command.Parameters.AddWithValue("$edgesJson", JsonSerializer.Serialize(graph.Edges, JsonOptions));
        command.ExecuteNonQuery();
    }

    private static void DeleteCitations(SqliteConnection connection, string sessionId, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM Citations WHERE SessionId = $sessionId;";
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.ExecuteNonQuery();
    }

    private static void DeleteGraphs(SqliteConnection connection, string sessionId, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM Graphs WHERE SessionId = $sessionId;";
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.ExecuteNonQuery();
    }

    private static (SessionSummary summary, bool exists)? LoadSession(SqliteConnection connection, string sessionId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT SessionId, Title, CreatedUtc, LastAccessUtc
            FROM Sessions
            WHERE SessionId = $sessionId;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? (ReadSessionSummary(reader), true) : null;
    }

    private static IReadOnlyList<MessageDto> LoadMessages(SqliteConnection connection, string sessionId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Role, Content, CreatedUtc
            FROM Messages
            WHERE SessionId = $sessionId
            ORDER BY MessageId ASC;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);
        using var reader = command.ExecuteReader();
        var messages = new List<MessageDto>();
        while (reader.Read())
        {
            messages.Add(new MessageDto(reader.GetString(0), reader.GetString(1), DateTime.Parse(reader.GetString(2), null, DateTimeStyles.RoundtripKind)));
        }

        return messages;
    }

    private static IReadOnlyList<CitationDto> LoadCitations(SqliteConnection connection, string sessionId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Title, Url, Section
            FROM Citations
            WHERE SessionId = $sessionId
            ORDER BY CitationId ASC;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);
        using var reader = command.ExecuteReader();
        var citations = new List<CitationDto>();
        while (reader.Read())
        {
            citations.Add(new CitationDto(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        return citations;
    }

    private static IReadOnlyList<GraphDto> LoadGraphs(SqliteConnection connection, string sessionId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Topic, NodesJson, EdgesJson
            FROM Graphs
            WHERE SessionId = $sessionId
            ORDER BY GraphId ASC;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);
        using var reader = command.ExecuteReader();
        var graphs = new List<GraphDto>();
        while (reader.Read())
        {
            // Graphs are stored as reloadable session artifacts, not ephemeral UI state.
            var nodes = JsonSerializer.Deserialize<List<GraphNodeDto>>(reader.GetString(1), JsonOptions) ?? [];
            var edges = JsonSerializer.Deserialize<List<GraphEdgeDto>>(reader.GetString(2), JsonOptions) ?? [];
            graphs.Add(new GraphDto(reader.GetString(0), nodes, edges));
        }

        return graphs;
    }

    private static SessionSummary ReadSessionSummary(SqliteDataReader reader) =>
        new(reader.GetString(0), reader.GetString(1), DateTime.Parse(reader.GetString(2), null, DateTimeStyles.RoundtripKind), DateTime.Parse(reader.GetString(3), null, DateTimeStyles.RoundtripKind));

    private static string InferTitle(string prompt)
    {
        var title = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(5);
        return string.Join(' ', title).Trim() is { Length: > 0 } value ? value : "New session";
    }

    private static string BuildStudyGuide(string prompt, string? sourceUrl)
    {
        var sourceText = string.IsNullOrWhiteSpace(sourceUrl) ? "Wikipedia" : sourceUrl;
        return $"""
                Study guide for {prompt}

                1. Core idea: treat the topic as a Wikipedia-first research starting point.
                2. Retrieval path: start with the source page, then follow linked articles and section headings.
                3. Notes: collect citations, then cluster related pages into a topic graph.
                4. Next action: refine the prompt into subtopics and read the cited pages directly.

                Source anchor: {sourceText}
                """;
    }

    private static IReadOnlyList<CitationDto> BuildCitations(string prompt, string? sourceUrl)
    {
        var slug = Uri.EscapeDataString(prompt.Trim().Replace(' ', '_'));
        var baseUrl = string.IsNullOrWhiteSpace(sourceUrl)
            ? $"https://en.wikipedia.org/wiki/{slug}"
            : sourceUrl.Trim();

        return
        [
            new CitationDto(prompt, baseUrl),
            new CitationDto($"{prompt} overview", $"https://en.wikipedia.org/wiki/{slug}", "Overview"),
            new CitationDto($"{prompt} references", $"https://en.wikipedia.org/wiki/{slug}#References", "References")
        ];
    }

    private static IReadOnlyList<GraphDto> BuildGraphs(string prompt)
    {
        var nodes = new List<GraphNodeDto>
        {
            new("topic", prompt, 5),
            new("history", "History", 3),
            new("related", "Related concepts", 2),
            new("sources", "Sources", 4)
        };

        var edges = new List<GraphEdgeDto>
        {
            new("topic", "history", "expands"),
            new("topic", "related", "links"),
            new("topic", "sources", "cites")
        };

        return [new GraphDto(prompt, nodes, edges)];
    }
}

internal static class SqliteConnectionExtensions
{
    public static void ExecuteNonQuery(this SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
