using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using WikiGraph.Api.Application.Models;
using WikiGraph.Contracts;

namespace WikiGraph.Api.Infrastructure.Persistence;

public sealed class SqliteSessionRepository : ISessionRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ISqliteConnectionFactory _connectionFactory;

    public SqliteSessionRepository(ISqliteConnectionFactory connectionFactory, SessionMemoryDb _)
    {
        _connectionFactory = connectionFactory;
    }

    public SessionSummary CreateSession(string title)
    {
        var nowUtc = DateTime.UtcNow;
        var session = new SessionSummary(Guid.NewGuid().ToString("N"), title, nowUtc, nowUtc);

        using var connection = _connectionFactory.OpenConnection();
        InsertSession(connection, session, SessionMemoryDb.DefaultUserId, null);
        return session;
    }

    public IReadOnlyList<SessionSummary> GetSessions()
    {
        using var connection = _connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT SessionId, Title, CreatedUtc, LastAccessUtc
            FROM Sessions
            WHERE UserId = $userId
            ORDER BY LastAccessUtc DESC;
            """;
        command.Parameters.AddWithValue("$userId", SessionMemoryDb.DefaultUserId);

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

    public IReadOnlyList<GraphDto>? GetGraphs(string sessionId)
    {
        using var connection = _connectionFactory.OpenConnection();
        return LoadSession(connection, sessionId) is null ? null : LoadGraphs(connection, sessionId);
    }

    public void SaveQueryArtifacts(QueryArtifacts artifacts)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var transaction = connection.BeginTransaction();

        var existingSession = LoadSession(connection, artifacts.SessionId);
        var accessedUtc = artifacts.AssistantMessage.CreatedUtc;
        if (existingSession is null)
        {
            InsertSession(
                connection,
                new SessionSummary(artifacts.SessionId, artifacts.SessionTitle, artifacts.UserMessage.CreatedUtc, accessedUtc),
                SessionMemoryDb.DefaultUserId,
                transaction);
        }
        else
        {
            var title = existingSession.Value.summary.Title == "New session"
                ? artifacts.SessionTitle
                : existingSession.Value.summary.Title;

            UpdateSession(connection, artifacts.SessionId, title, accessedUtc, transaction);
        }

        InsertMessage(connection, artifacts.SessionId, artifacts.UserMessage, transaction);
        InsertMessage(connection, artifacts.SessionId, artifacts.AssistantMessage, transaction);

        foreach (var citation in artifacts.Citations)
        {
            InsertCitation(connection, artifacts.SessionId, citation, transaction);
        }

        foreach (var graph in artifacts.Graphs)
        {
            InsertGraph(connection, artifacts.SessionId, graph, transaction);
        }

        transaction.Commit();
    }

    private static void InsertSession(SqliteConnection connection, SessionSummary session, string userId, SqliteTransaction? transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Sessions (SessionId, UserId, Title, CreatedUtc, LastAccessUtc)
            VALUES ($sessionId, $userId, $title, $createdUtc, $lastAccessUtc);
            """;
        command.Parameters.AddWithValue("$sessionId", session.SessionId);
        command.Parameters.AddWithValue("$userId", userId);
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
            messages.Add(new MessageDto(
                reader.GetString(0),
                reader.GetString(1),
                DateTime.Parse(reader.GetString(2), null, DateTimeStyles.RoundtripKind)));
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
            var nodes = JsonSerializer.Deserialize<List<GraphNodeDto>>(reader.GetString(1), JsonOptions) ?? [];
            var edges = JsonSerializer.Deserialize<List<GraphEdgeDto>>(reader.GetString(2), JsonOptions) ?? [];
            graphs.Add(new GraphDto(reader.GetString(0), nodes, edges));
        }

        return graphs;
    }

    private static SessionSummary ReadSessionSummary(SqliteDataReader reader) =>
        new(
            reader.GetString(0),
            reader.GetString(1),
            DateTime.Parse(reader.GetString(2), null, DateTimeStyles.RoundtripKind),
            DateTime.Parse(reader.GetString(3), null, DateTimeStyles.RoundtripKind));
}
