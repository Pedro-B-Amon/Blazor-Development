using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WikiGraph.Api.Configuration;
using WikiGraph.Api.Application.Models;

namespace WikiGraph.Api.Infrastructure.Persistence;

public sealed class SqliteVectorStore : IVectorStore
{
    private const string KeywordEncodingKind = "keyword-json";
    private const string VectorEncodingKind = "float32-json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly IEmbeddingGenerator<string, Embedding<float>>? _embeddingGenerationService;
    private readonly OpenAIOptions _options;
    private readonly ILogger<SqliteVectorStore> _logger;

    public SqliteVectorStore(
        ISqliteConnectionFactory connectionFactory,
        SessionMemoryDb _,
        IServiceProvider serviceProvider,
        IOptions<OpenAIOptions> options,
        ILogger<SqliteVectorStore> logger)
    {
        _connectionFactory = connectionFactory;
        _embeddingGenerationService = serviceProvider.GetService<IEmbeddingGenerator<string, Embedding<float>>>();
        _options = options.Value;
        _logger = logger;
    }

    public async Task UpsertAsync(string sessionId, WikipediaPage page, IReadOnlyList<WikipediaChunk> chunks, CancellationToken cancellationToken = default)
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
            var embedding = await CreateEmbeddingAsync(chunk, cancellationToken);
            InsertEmbedding(connection, storedChunkId, embedding, transaction);
        }

        transaction.Commit();
    }

    public async Task<IReadOnlyList<RetrievedContext>> SearchAsync(string sessionId, string prompt, int count, CancellationToken cancellationToken = default)
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

        var promptTerms = TextTokenizer.ExtractTerms(prompt).ToHashSet(StringComparer.Ordinal);
        var promptEmbedding = await CreatePromptEmbeddingAsync(prompt, cancellationToken);
        using var reader = command.ExecuteReader();
        var results = new List<RetrievedContext>();
        while (reader.Read())
        {
            var encodingKind = reader.IsDBNull(5) ? KeywordEncodingKind : reader.GetString(5);
            var score = ScoreCandidate((byte[])reader["Embedding"], encodingKind, promptTerms, promptEmbedding)
                + (reader.GetString(1).Equals("Overview", StringComparison.OrdinalIgnoreCase) ? 0.25 : 0d);

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

    private async Task<StoredEmbedding> CreateEmbeddingAsync(WikipediaChunk chunk, CancellationToken cancellationToken)
    {
        if (_embeddingGenerationService is null || !_options.IsEnabled)
        {
            return BuildKeywordEmbedding(chunk.Terms);
        }

        try
        {
            var embedding = await _embeddingGenerationService.GenerateVectorAsync(chunk.Text, cancellationToken: cancellationToken);
            var values = embedding.ToArray();
            return new StoredEmbedding(VectorEncodingKind, _options.EmbeddingModelId, values.Length, EncodeVector(values));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI embedding generation failed for chunk {ChunkId}; using keyword retrieval fallback.", chunk.ChunkId);
            return BuildKeywordEmbedding(chunk.Terms);
        }
    }

    private async Task<float[]?> CreatePromptEmbeddingAsync(string prompt, CancellationToken cancellationToken)
    {
        if (_embeddingGenerationService is null || !_options.IsEnabled || string.IsNullOrWhiteSpace(prompt))
        {
            return null;
        }

        try
        {
            var embedding = await _embeddingGenerationService.GenerateVectorAsync(prompt, cancellationToken: cancellationToken);
            return embedding.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI embedding generation failed for the retrieval prompt; using keyword overlap.");
            return null;
        }
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

    private static void InsertEmbedding(SqliteConnection connection, string chunkId, StoredEmbedding embedding, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO ChunkEmbeddings (ChunkId, Embedding, EncodingKind, ModelId, Dimensions)
            VALUES ($chunkId, $embedding, $encodingKind, $modelId, $dimensions);
            """;
        command.Parameters.AddWithValue("$chunkId", chunkId);
        command.Parameters.AddWithValue("$embedding", embedding.Payload);
        command.Parameters.AddWithValue("$encodingKind", embedding.EncodingKind);
        command.Parameters.AddWithValue("$modelId", (object?)embedding.ModelId ?? DBNull.Value);
        command.Parameters.AddWithValue("$dimensions", (object?)embedding.Dimensions ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    private static StoredEmbedding BuildKeywordEmbedding(IReadOnlyList<string> terms) =>
        new(KeywordEncodingKind, "keyword-overlap-v1", terms.Count, EncodeTerms(terms));

    private static byte[] EncodeTerms(IReadOnlyList<string> terms) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(terms, JsonOptions));

    private static IReadOnlyList<string> DecodeTerms(byte[] payload) =>
        JsonSerializer.Deserialize<string[]>(Encoding.UTF8.GetString(payload), JsonOptions) ?? [];

    private static byte[] EncodeVector(IReadOnlyList<float> vector) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(vector, JsonOptions));

    private static float[] DecodeVector(byte[] payload) =>
        JsonSerializer.Deserialize<float[]>(Encoding.UTF8.GetString(payload), JsonOptions) ?? [];

    private static double ScoreCandidate(
        byte[] payload,
        string encodingKind,
        IReadOnlySet<string> promptTerms,
        float[]? promptEmbedding)
    {
        if (string.Equals(encodingKind, VectorEncodingKind, StringComparison.OrdinalIgnoreCase) && promptEmbedding is not null)
        {
            return CosineSimilarity(promptEmbedding, DecodeVector(payload));
        }

        var terms = DecodeTerms(payload);
        return promptTerms.Count == 0 ? 0d : terms.Count(promptTerms.Contains);
    }

    private static double CosineSimilarity(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        var length = Math.Min(left.Count, right.Count);
        if (length == 0)
        {
            return 0d;
        }

        double dot = 0;
        double leftMagnitude = 0;
        double rightMagnitude = 0;

        for (var index = 0; index < length; index++)
        {
            dot += left[index] * right[index];
            leftMagnitude += left[index] * left[index];
            rightMagnitude += right[index] * right[index];
        }

        if (leftMagnitude == 0 || rightMagnitude == 0)
        {
            return 0d;
        }

        return dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }

    private static string BuildStoredPageId(string sessionId, string pageId) => $"{sessionId}:{pageId}";

    private static string BuildStoredChunkId(string sessionId, string chunkId) => $"{sessionId}:{chunkId}";
}
