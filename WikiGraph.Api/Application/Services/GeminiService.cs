using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.ChatCompletion;
using WikiGraph.Api.Application.Models;
using WikiGraph.Api.Configuration;
using WikiGraph.Contracts;

namespace WikiGraph.Api.Application.Services;

public sealed class GeminiService
{
    private readonly IChatCompletionService? _chatCompletionService;
    private readonly IEmbeddingGenerator<string, Embedding<float>>? _embeddingGenerator;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiService> _logger;

    // Creates a service instance with Gemini settings but without optional SK integrations.
    public GeminiService(IOptions<GeminiOptions> options, ILogger<GeminiService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    // Creates a service instance and resolves optional chat/embedding services from DI.
    public GeminiService(
        IServiceProvider serviceProvider,
        IOptions<GeminiOptions> options,
        ILogger<GeminiService> logger)
        : this(options, logger)
    {
        _chatCompletionService = serviceProvider.GetService<IChatCompletionService>();
        _embeddingGenerator = serviceProvider.GetService<IEmbeddingGenerator<string, Embedding<float>>>();
    }

    // Generates the answer and related topic labels, falling back to local text if Gemini is unavailable.
    public async Task<GeminiReply> GenerateReplyAsync(
        string prompt,
        WikiArticle article,
        IReadOnlyList<MessageDto> sessionHistory,
        IReadOnlyList<WikiMatch> matches,
        CancellationToken cancellationToken = default)
    {
        // Keep the AI path simple: one Gemini call returns the answer text and graph labels together.
        var fallback = BuildFallbackReply(prompt, article, sessionHistory, matches);
        if (!_options.IsEnabled)
        {
            return fallback;
        }

        try
        {
            if (_chatCompletionService is null)
            {
                return fallback;
            }

            var completions = await _chatCompletionService.GetChatMessageContentsAsync(
                BuildChatHistory(prompt, article, sessionHistory, matches),
                executionSettings: null,
                kernel: null,
                cancellationToken);

            var text = completions.FirstOrDefault()?.Content;
            if (ReadReply(text, article, prompt, matches) is { } reply)
            {
                return reply;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini Semantic Kernel reply request failed; using local fallback text.");
        }

        return fallback;
    }

    // Creates an embedding vector for retrieval, or null when embeddings are disabled.
    public async Task<float[]?> CreateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (!_options.IsEnabled || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            if (_embeddingGenerator is null)
            {
                return null;
            }

            var vector = await _embeddingGenerator.GenerateVectorAsync(text, cancellationToken: cancellationToken);
            return vector.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini Semantic Kernel embedding request failed; using keyword retrieval.");
            return null;
        }
    }

    // Builds the chat prompt that asks Gemini for JSON-only output.
    private static ChatHistory BuildChatHistory(
        string prompt,
        WikiArticle article,
        IReadOnlyList<MessageDto> sessionHistory,
        IReadOnlyList<WikiMatch> matches)
    {
        var history = new ChatHistory(
            """
            You are a session-aware Wikipedia research assistant inside a RAG application.
            Use only the supplied article summary, retrieved sections, and prior session messages.
            Return strict JSON only with:
            - "answer": a grounded response for the user
            - "relatedTopics": an array of 2 to 4 short labels

            Rules:
            - Be specific and useful, not generic.
            - If the input is a topic, produce a concise study guide grounded in the article and retrieved context.
            - If the input is a question, answer it directly using the provided context.
            - Mention relationships, definitions, and follow-up reading when helpful.
            - Do not use markdown code fences.
            """
        );

        foreach (var message in sessionHistory.TakeLast(6))
        {
            if (string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            {
                history.AddAssistantMessage(message.Content);
                continue;
            }

            history.AddUserMessage(message.Content);
        }

        var matchLines = matches
            .Take(4)
            .Select(match => $"- [{match.ChunkId}] {match.Section}: {TextTools.TrimToLength(match.Text, 220)}")
            .DefaultIfEmpty("- no extra sections were retrieved");
        var relatedTopicLines = article.RelatedTopicDetails
            .Take(4)
            .Select(topic => $"- {topic.Title}: {topic.Summary}")
            .DefaultIfEmpty("- no related-topic summaries were available");

        history.AddUserMessage($"""
            User input: {prompt}
            Canonical article: {article.Title}
            Article URL: {article.SourceUrl}
            Article summary: {article.Summary}
            Related Wikipedia articles: {string.Join(", ", article.RelatedArticles.Take(6))}

            Related topic details:
            {string.Join(Environment.NewLine, relatedTopicLines)}

            Retrieved context:
            {string.Join(Environment.NewLine, matchLines)}

            Respond with valid JSON only.
            """);

        return history;
    }

    // Builds a local answer when Gemini is off or unavailable.
    private static GeminiReply BuildFallbackReply(
        string prompt,
        WikiArticle article,
        IReadOnlyList<MessageDto> sessionHistory,
        IReadOnlyList<WikiMatch> matches)
    {
        var priorMessages = sessionHistory
            .Where(message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
            .TakeLast(2)
            .Select(message => message.Content)
            .ToArray();
        var relatedTopics = article.RelatedArticles.Take(4).DefaultIfEmpty($"{article.Title} references").ToArray();
        var keyIdeas = matches.Count > 0
            ? string.Join(", ", matches.Take(3).Select(match => match.Section.ToLowerInvariant()))
            : string.Join(", ", relatedTopics);
        var relatedTopicNotes = article.RelatedTopicDetails.Count > 0
            ? string.Join(
                Environment.NewLine,
                article.RelatedTopicDetails
                    .Take(4)
                    .Select(topic => $"- {topic.Title}: {topic.Summary}"))
            : string.Join(Environment.NewLine, relatedTopics.Select(topic => $"- {topic}: related context for {article.Title}."));

        return new GeminiReply(
            $"""
            Overview: {article.Summary}

            Key ideas: {keyIdeas}.

            Related topics:
            {relatedTopicNotes}

            Prior focus: {(priorMessages.Length == 0 ? "this is the first turn in the session." : string.Join(" | ", priorMessages))}
            """,
            BuildFallbackTopics(prompt, article, matches));
    }

    // Parses Gemini JSON output into the reply model.
    private static GeminiReply? ReadReply(
        string? responseText,
        WikiArticle article,
        string prompt,
        IReadOnlyList<WikiMatch> matches)
    {
        var text = TextTools.Clean(responseText);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (TryParseReply(text, prompt, article, matches) is { } reply)
        {
            return reply;
        }

        return new GeminiReply(TextTools.Clean(text), BuildFallbackTopics(prompt, article, matches));
    }

    // Parses the JSON answer and related topics from the model response.
    private static GeminiReply? TryParseReply(
        string text,
        string prompt,
        WikiArticle article,
        IReadOnlyList<WikiMatch> matches)
    {
        try
        {
            using var document = JsonDocument.Parse(StripCodeFence(text));
            if (!document.RootElement.TryGetProperty("answer", out var answerElement) ||
                answerElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var relatedTopics = new List<string>();
            if (document.RootElement.TryGetProperty("relatedTopics", out var topicsElement) &&
                topicsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var topic in topicsElement.EnumerateArray())
                {
                    if (topic.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var value = TextTools.Clean(topic.GetString());
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        relatedTopics.Add(value);
                    }
                }
            }

            return new GeminiReply(
                TextTools.Clean(answerElement.GetString()),
                relatedTopics.Count == 0 ? BuildFallbackTopics(prompt, article, matches) : relatedTopics);
        }
        catch
        {
            return null;
        }
    }

    // Builds a safe fallback topic list from the prompt, article, and matches.
    private static IReadOnlyList<string> BuildFallbackTopics(
        string prompt,
        WikiArticle article,
        IReadOnlyList<WikiMatch> matches)
    {
        return new[] { prompt, article.Title }
            .Concat(article.RelatedArticles)
            .Concat(matches.Select(match => match.Section))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(TextTools.Clean)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();
    }

    // Removes markdown code fences around JSON payloads.
    private static string StripCodeFence(string text)
    {
        var cleaned = text.Trim();
        if (!cleaned.StartsWith("```", StringComparison.Ordinal))
        {
            return cleaned;
        }

        var firstLineBreak = cleaned.IndexOf('\n');
        if (firstLineBreak < 0)
        {
            return cleaned.Trim('`').Trim();
        }

        var content = cleaned[(firstLineBreak + 1)..];
        var closingFence = content.LastIndexOf("```", StringComparison.Ordinal);
        return closingFence < 0 ? content.Trim() : content[..closingFence].Trim();
    }

    // Extracts the first text part from Gemini's JSON response shape.
    private static string? ReadGeneratedText(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (!candidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                {
                    return TextTools.Clean(text.GetString());
                }
            }
        }

        return null;
    }
}

public sealed record GeminiReply(string Answer, IReadOnlyList<string> RelatedTopics);
