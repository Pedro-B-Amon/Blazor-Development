using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.ChatCompletion;
using WikiGraph.Api.Configuration;
using WikiGraph.Api.Application.Abstractions;
using WikiGraph.Api.Application.Models;
using WikiGraph.Contracts;

namespace WikiGraph.Api.Application.Services;

/// <summary>
/// Produces a grounded answer, preferring the model when available and falling back to deterministic text otherwise.
/// </summary>
public sealed class AISummarizer : IAISummarizer
{
    private readonly IChatCompletionService? _chatCompletionService;
    private readonly OpenAIOptions _options;
    private readonly ILogger<AISummarizer> _logger;

    public AISummarizer(IServiceProvider serviceProvider, IOptions<OpenAIOptions> options, ILogger<AISummarizer> logger)
    {
        _chatCompletionService = serviceProvider.GetService<IChatCompletionService>();
        _options = options.Value;
        _logger = logger;
    }

    public async Task<GeneratedAnswer> GenerateAsync(
        QueryRequest request,
        string effectivePrompt,
        WikipediaPage page,
        IReadOnlyList<MessageDto> sessionHistory,
        IReadOnlyList<RetrievedContext> context,
        CancellationToken cancellationToken = default)
    {
        var citations = BuildCitations(page, context);
        var fallbackText = BuildFallbackText(effectivePrompt, page, sessionHistory, context, citations);

        // If the chat service is unavailable, the fallback path still returns a useful, fully grounded response.
        if (_chatCompletionService is null || !_options.IsEnabled)
        {
            return new GeneratedAnswer(fallbackText, citations);
        }

        try
        {
            var chatHistory = BuildChatHistory(effectivePrompt, page, sessionHistory, context, citations);
            var completions = await _chatCompletionService.GetChatMessageContentsAsync(
                chatHistory,
                executionSettings: null,
                kernel: null,
                cancellationToken);

            var assistantText = completions.FirstOrDefault()?.Content?.Trim();
            if (!string.IsNullOrWhiteSpace(assistantText))
            {
                return new GeneratedAnswer(assistantText, citations);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Semantic Kernel summarization failed; falling back to deterministic output.");
        }

        return new GeneratedAnswer(fallbackText, citations);
    }

    private static string BuildFallbackText(
        string effectivePrompt,
        WikipediaPage page,
        IReadOnlyList<MessageDto> sessionHistory,
        IReadOnlyList<RetrievedContext> context,
        IReadOnlyList<CitationDto> citations)
    {
        var selectedContext = context.Take(3).ToArray();
        var concepts = selectedContext.Length > 0
            ? string.Join(", ", selectedContext.Select(item => item.Section.ToLowerInvariant()))
            : "overview, history, and related concepts";
        var conversationContext = sessionHistory
            .Where(message => !string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            .TakeLast(2)
            .Select(message => message.Content)
            .ToArray();
        var priorContextLine = conversationContext.Length > 0
            ? $"Prior focus: {string.Join(" | ", conversationContext)}."
            : "Prior focus: this is the first turn in the session.";

        var notes = selectedContext.Length > 0
            ? selectedContext.Select((item, index) => $"{index + 1}. {item.Section}: {BuildSentence(item.Text)}")
            : ["1. Overview: Start with the main article summary and expand into linked sections."];

        var assistantText = $"""
            Study guide for {effectivePrompt}

            Core concepts: {concepts}.
            Research focus: anchor your notes on {page.Title}, compare the highlighted sections, and branch into linked topics.
            {priorContextLine}

            Key notes:
            {string.Join(Environment.NewLine, notes)}

            Next step: follow the citations below to expand the graph with narrower Wikipedia subtopics.

            Source anchor: {page.SourceUrl}
            Retrieved chunks: {string.Join(", ", citations.Select(citation => citation.ChunkId ?? "page-anchor").Distinct())}
            """;

        return assistantText;
    }

    private static IReadOnlyList<CitationDto> BuildCitations(WikipediaPage page, IReadOnlyList<RetrievedContext> context)
    {
        var citations = context.Take(3)
            .Select(item => new CitationDto(
                item.Section.Equals("Overview", StringComparison.OrdinalIgnoreCase) ? page.Title : $"{page.Title} {item.Section}",
                item.Section.Equals("Overview", StringComparison.OrdinalIgnoreCase) ? page.SourceUrl : $"{page.SourceUrl}#{TextTokenizer.Slugify(item.Section)}",
                item.Section,
                item.ChunkId))
            .Distinct()
            .ToArray();

        return citations.Length > 0 ? citations : BuildDefaultCitations(page);
    }

    private static IReadOnlyList<CitationDto> BuildDefaultCitations(WikipediaPage page) =>
    [
        new CitationDto(page.Title, page.SourceUrl, "Overview", null),
        new CitationDto($"{page.Title} references", $"{page.SourceUrl}#references", "References", null),
        new CitationDto($"{page.Title} related topics", $"{page.SourceUrl}#related-topics", "Related topics", null)
    ];

    private static ChatHistory BuildChatHistory(
        string effectivePrompt,
        WikipediaPage page,
        IReadOnlyList<MessageDto> sessionHistory,
        IReadOnlyList<RetrievedContext> context,
        IReadOnlyList<CitationDto> citations)
    {
        var history = new ChatHistory(
            """
            You are producing a concise Wikipedia-grounded study guide.
            Use only the supplied session history and retrieved context.
            Structure the answer with short titled sections, emphasize definitions, relationships, and research directions, and avoid inventing sources.
            Mention chunk identifiers inline when helpful, but do not fabricate any identifiers beyond those provided.
            """);

        // Preserve only the last few turns so the prompt stays small and the chat model sees the most recent thread state.
        foreach (var message in sessionHistory.TakeLast(6))
        {
            if (string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            {
                history.AddAssistantMessage(message.Content);
                continue;
            }

            history.AddUserMessage(message.Content);
        }

        var retrievedSections = context.Take(4)
            .Select(item => $"- [{item.ChunkId}] {item.Section}: {BuildSentence(item.Text)}")
            .DefaultIfEmpty("- No retrieved chunks were available; rely on the page anchor and general study framing.");

        var citationLines = citations
            .Select(citation => $"- {citation.Title} | {citation.Url} | chunk={citation.ChunkId ?? "page-anchor"}")
            .ToArray();

        history.AddUserMessage($"""
            Topic or URL focus: {effectivePrompt}
            Canonical article: {page.Title}
            Source URL: {page.SourceUrl}

            Retrieved context:
            {string.Join(Environment.NewLine, retrievedSections)}

            Available citations:
            {string.Join(Environment.NewLine, citationLines)}

            Write a study-guide style response with these sections:
            1. Overview
            2. Key ideas
            3. Connections and follow-up reading

            Keep it grounded to the provided article and context.
            """);

        return history;
    }

    private static string BuildSentence(string text)
    {
        var normalized = text.ReplaceLineEndings(" ").Trim();
        if (normalized.Length <= 110)
        {
            return normalized;
        }

        return $"{normalized[..107].TrimEnd()}...";
    }
}
