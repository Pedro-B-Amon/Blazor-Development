using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using WikiGraph.Api.Application.Models;
using WikiGraph.Api.Application.Services;

namespace WikiGraph.Api.Infrastructure.Persistence;

public sealed class SqliteVectorStore
{
    private const string KeywordEncodingKind = "keyword-json";
    private const string GeminiEncodingKind = "gemini-float32-json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly GeminiService _geminiService;
    private readonly ILogger<SqliteVectorStore> _logger;

    public SqliteVectorStore(
        ISqliteConnectionFactory connectionFactory,
        SessionMemoryDb _,
        GeminiService geminiService,
        ILogger<SqliteVectorStore> logger)
    {
        _connectionFactory = connectionFactory;
        _geminiService = geminiService;
        _logger = logger;
    }

    public async Task UpsertArticleAsync(string sessionId, WikiArticle article, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var transaction = connection.BeginTransaction();

        var storedArticleId = $"{sessionId}:{article.ArticleId}";
        DeleteStoredArticle(connection, storedArticleId, transaction);
        InsertArticle(connection, sessionId, storedArticleId, article, transaction);

        foreach (var item in article.Sections
            .Where(section => !string.IsNullOrWhiteSpace(section.Content))
            .Select((section, index) => new { section, index }))
        {
            var sectionText = $"{article.Title}: {item.section.Content}";
            var chunkId = BuildChunkId(storedArticleId, item.section, item.index, sectionText);
            InsertSection(connection, storedArticleId, chunkId, item.section, sectionText, transaction);
            await InsertEmbeddingAsync(connection, chunkId, sectionText, cancellationToken, transaction);
        }

        transaction.Commit();
    }

    public async Task<IReadOnlyList<WikiMatch>> SearchAsync(string sessionId, string text, int count, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.ChunkId, c.Section, c.Text, p.SourceUrl, e.Embedding, e.EncodingKind
            FROM DocumentChunks c
            INNER JOIN WikipediaPages p ON p.PageId = c.PageId
            INNER JOIN ChunkEmbeddings e ON e.ChunkId = c.ChunkId
            WHERE p.SessionId = $sessionId;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);

        var queryTerms = TextTools.ExtractTerms(text).ToHashSet(StringComparer.Ordinal);
        var queryVector = await _geminiService.CreateEmbeddingAsync(text, cancellationToken);
        using var reader = command.ExecuteReader();
        var matches = new List<WikiMatch>();

        while (reader.Read())
        {
            var payload = (byte[])reader["Embedding"];
            var encodingKind = reader.IsDBNull(5) ? KeywordEncodingKind : reader.GetString(5);
            var score = Score(payload, encodingKind, queryTerms, queryVector);
            if (reader.GetString(1).Equals("Overview", StringComparison.OrdinalIgnoreCase))
            {
                score += 0.25d;
            }

            matches.Add(new WikiMatch(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                score));
        }

        return matches
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Section, StringComparer.OrdinalIgnoreCase)
            .Take(count)
            .ToArray();
    }

    private async Task InsertEmbeddingAsync(
        SqliteConnection connection,
        string chunkId,
        string sectionText,
        CancellationToken cancellationToken,
        SqliteTransaction transaction)
    {
        var vector = await _geminiService.CreateEmbeddingAsync(sectionText, cancellationToken);
        var encodingKind = vector is null ? KeywordEncodingKind : GeminiEncodingKind;
        var payload = vector is null
            ? Encoding.UTF8.GetBytes(JsonSerializer.Serialize(TextTools.ExtractTerms(sectionText), JsonOptions))
            : Encoding.UTF8.GetBytes(JsonSerializer.Serialize(vector, JsonOptions));

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO ChunkEmbeddings (ChunkId, Embedding, EncodingKind, ModelId, Dimensions)
            VALUES ($chunkId, $embedding, $encodingKind, $modelId, $dimensions);
            """;
        command.Parameters.AddWithValue("$chunkId", chunkId);
        command.Parameters.AddWithValue("$embedding", payload);
        command.Parameters.AddWithValue("$encodingKind", encodingKind);
        command.Parameters.AddWithValue("$modelId", vector is null ? "keyword-overlap-v1" : "gemini");
        command.Parameters.AddWithValue("$dimensions", vector?.Length is int length ? length : DBNull.Value);
        command.ExecuteNonQuery();
    }

    private static void DeleteStoredArticle(SqliteConnection connection, string articleId, SqliteTransaction transaction)
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
        deleteEmbeddings.Parameters.AddWithValue("$pageId", articleId);
        deleteEmbeddings.ExecuteNonQuery();

        using var deleteChunks = connection.CreateCommand();
        deleteChunks.Transaction = transaction;
        deleteChunks.CommandText = "DELETE FROM DocumentChunks WHERE PageId = $pageId;";
        deleteChunks.Parameters.AddWithValue("$pageId", articleId);
        deleteChunks.ExecuteNonQuery();

        using var deleteArticle = connection.CreateCommand();
        deleteArticle.Transaction = transaction;
        deleteArticle.CommandText = "DELETE FROM WikipediaPages WHERE PageId = $pageId;";
        deleteArticle.Parameters.AddWithValue("$pageId", articleId);
        deleteArticle.ExecuteNonQuery();
    }

    private static void InsertArticle(SqliteConnection connection, string sessionId, string articleId, WikiArticle article, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO WikipediaPages (PageId, SessionId, Title, SourceUrl, RetrievedUtc)
            VALUES ($pageId, $sessionId, $title, $sourceUrl, $retrievedUtc);
            """;
        command.Parameters.AddWithValue("$pageId", articleId);
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.Parameters.AddWithValue("$title", article.Title);
        command.Parameters.AddWithValue("$sourceUrl", article.SourceUrl);
        command.Parameters.AddWithValue("$retrievedUtc", article.RetrievedUtc.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static void InsertSection(
        SqliteConnection connection,
        string articleId,
        string chunkId,
        WikiSection section,
        string text,
        SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO DocumentChunks (ChunkId, PageId, Section, Text, Hash)
            VALUES ($chunkId, $pageId, $section, $text, $hash);
            """;
        command.Parameters.AddWithValue("$chunkId", chunkId);
        command.Parameters.AddWithValue("$pageId", articleId);
        command.Parameters.AddWithValue("$section", section.Heading);
        command.Parameters.AddWithValue("$text", text);
        command.Parameters.AddWithValue("$hash", TextTools.Hash(text));
        command.ExecuteNonQuery();
    }

    private static string BuildChunkId(string articleId, WikiSection section, int index, string text)
    {
        // Section headings can repeat (for example "Key Point"), so include the position and a short content hash.
        var headingSlug = TextTools.Slugify(section.Heading);
        var hash = TextTools.Hash(text)[..8].ToLowerInvariant();
        return $"{articleId}:{index + 1:D2}:{headingSlug}:{hash}";
    }

    private double Score(byte[] payload, string encodingKind, IReadOnlySet<string> queryTerms, float[]? queryVector)
    {
        try
        {
            if (string.Equals(encodingKind, GeminiEncodingKind, StringComparison.OrdinalIgnoreCase) && queryVector is not null)
            {
                var storedVector = JsonSerializer.Deserialize<float[]>(Encoding.UTF8.GetString(payload), JsonOptions) ?? [];
                return CosineSimilarity(queryVector, storedVector);
            }

            var storedTerms = JsonSerializer.Deserialize<string[]>(Encoding.UTF8.GetString(payload), JsonOptions) ?? [];
            return queryTerms.Count == 0 ? 0d : storedTerms.Count(queryTerms.Contains);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Stored embedding payload could not be scored.");
            return 0d;
        }
    }

    private static double CosineSimilarity(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        var length = Math.Min(left.Count, right.Count);
        if (length == 0)
        {
            return 0d;
        }

        double dot = 0d;
        double leftSize = 0d;
        double rightSize = 0d;

        for (var index = 0; index < length; index++)
        {
            dot += left[index] * right[index];
            leftSize += left[index] * left[index];
            rightSize += right[index] * right[index];
        }

        if (leftSize == 0d || rightSize == 0d)
        {
            return 0d;
        }

        return dot / (Math.Sqrt(leftSize) * Math.Sqrt(rightSize));
    }
}
