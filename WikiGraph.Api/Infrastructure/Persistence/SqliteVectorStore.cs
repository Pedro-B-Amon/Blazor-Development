using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using WikiGraph.Api.Application.Models;

namespace WikiGraph.Api.Infrastructure.Persistence;

public sealed class SqliteVectorStore : IVectorStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ISqliteConnectionFactory _connectionFactory;

    public SqliteVectorStore(ISqliteConnectionFactory connectionFactory, SessionMemoryDb _)
    {
        _connectionFactory = connectionFactory;
    }

    public void Upsert(string sessionId, WikipediaPage page, IReadOnlyList<WikipediaChunk> chunks)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var transaction = connection.BeginTransaction();

        var storedPageId = BuildStoredPageId(sessionId, page.PageId);
        DeletePageArtifacts(connection, storedPageId, transaction);
        InsertPage(connection, sessionId, storedPageId, page, transaction);
        foreach (var chunk in chunks)
        {
            var storedChunkId = BuildStoredChunkId(sessionId, chunk.ChunkId);
            InsertChunk(connection, storedPageId, storedChunkId, chunk, transaction);
            InsertEmbedding(connection, storedChunkId, EncodeTerms(chunk.Terms), transaction);
        }

        transaction.Commit();
    }

    public IReadOnlyList<RetrievedContext> Search(string sessionId, string prompt, int count)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.ChunkId, c.Section, c.Text, p.SourceUrl, e.Embedding
            FROM DocumentChunks c
            INNER JOIN WikipediaPages p ON p.PageId = c.PageId
            INNER JOIN ChunkEmbeddings e ON e.ChunkId = c.ChunkId
            WHERE p.SessionId = $sessionId;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);

        var promptTerms = TextTokenizer.ExtractTerms(prompt).ToHashSet(StringComparer.Ordinal);
        using var reader = command.ExecuteReader();
        var results = new List<RetrievedContext>();
        while (reader.Read())
        {
            var terms = DecodeTerms((byte[])reader["Embedding"]);
            var overlap = promptTerms.Count == 0 ? 0 : terms.Count(promptTerms.Contains);
            var score = overlap + (reader.GetString(1).Equals("Overview", StringComparison.OrdinalIgnoreCase) ? 0.25 : 0d);

            results.Add(new RetrievedContext(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                score));
        }

        return results
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Section, StringComparer.OrdinalIgnoreCase)
            .Take(count)
            .ToArray();
    }

    private static void DeletePageArtifacts(SqliteConnection connection, string pageId, SqliteTransaction transaction)
    {
        using var deleteEmbeddings = connection.CreateCommand();
        deleteEmbeddings.Transaction = transaction;
        deleteEmbeddings.CommandText = """
            DELETE FROM ChunkEmbeddings
            WHERE ChunkId IN (
                SELECT ChunkId
                FROM DocumentChunks
                WHERE PageId = $pageId
            );
            """;
        deleteEmbeddings.Parameters.AddWithValue("$pageId", pageId);
        deleteEmbeddings.ExecuteNonQuery();

        using var deleteChunks = connection.CreateCommand();
        deleteChunks.Transaction = transaction;
        deleteChunks.CommandText = "DELETE FROM DocumentChunks WHERE PageId = $pageId;";
        deleteChunks.Parameters.AddWithValue("$pageId", pageId);
        deleteChunks.ExecuteNonQuery();

        using var deletePage = connection.CreateCommand();
        deletePage.Transaction = transaction;
        deletePage.CommandText = "DELETE FROM WikipediaPages WHERE PageId = $pageId;";
        deletePage.Parameters.AddWithValue("$pageId", pageId);
        deletePage.ExecuteNonQuery();
    }

    private static void InsertPage(SqliteConnection connection, string sessionId, string storedPageId, WikipediaPage page, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO WikipediaPages (PageId, SessionId, Title, SourceUrl, RetrievedUtc)
            VALUES ($pageId, $sessionId, $title, $sourceUrl, $retrievedUtc);
            """;
        command.Parameters.AddWithValue("$pageId", storedPageId);
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.Parameters.AddWithValue("$title", page.Title);
        command.Parameters.AddWithValue("$sourceUrl", page.SourceUrl);
        command.Parameters.AddWithValue("$retrievedUtc", page.RetrievedUtc.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static void InsertChunk(SqliteConnection connection, string storedPageId, string storedChunkId, WikipediaChunk chunk, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO DocumentChunks (ChunkId, PageId, Section, Text, Hash)
            VALUES ($chunkId, $pageId, $section, $text, $hash);
            """;
        command.Parameters.AddWithValue("$chunkId", storedChunkId);
        command.Parameters.AddWithValue("$pageId", storedPageId);
        command.Parameters.AddWithValue("$section", chunk.Section);
        command.Parameters.AddWithValue("$text", chunk.Text);
        command.Parameters.AddWithValue("$hash", chunk.Hash);
        command.ExecuteNonQuery();
    }

    private static void InsertEmbedding(SqliteConnection connection, string chunkId, byte[] embedding, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO ChunkEmbeddings (ChunkId, Embedding)
            VALUES ($chunkId, $embedding);
            """;
        command.Parameters.AddWithValue("$chunkId", chunkId);
        command.Parameters.AddWithValue("$embedding", embedding);
        command.ExecuteNonQuery();
    }

    private static byte[] EncodeTerms(IReadOnlyList<string> terms) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(terms, JsonOptions));

    private static IReadOnlyList<string> DecodeTerms(byte[] payload) =>
        JsonSerializer.Deserialize<string[]>(Encoding.UTF8.GetString(payload), JsonOptions) ?? [];

    private static string BuildStoredPageId(string sessionId, string pageId) => $"{sessionId}:{pageId}";

    private static string BuildStoredChunkId(string sessionId, string chunkId) => $"{sessionId}:{chunkId}";
}
