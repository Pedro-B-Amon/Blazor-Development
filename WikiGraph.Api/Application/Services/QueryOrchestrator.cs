using WikiGraph.Api.Application.Abstractions;
using WikiGraph.Api.Application.Models;
using WikiGraph.Api.Infrastructure.Persistence;
using WikiGraph.Api.Infrastructure.Wikipedia;
using WikiGraph.Contracts;

namespace WikiGraph.Api.Application.Services;

/// <summary>
/// Coordinates the full query flow: resolve the article, ingest chunks, retrieve context, summarize, and persist.
/// </summary>
public sealed class QueryOrchestrator : IQueryOrchestrator
{
    private readonly IWikiClient _wikiClient;
    private readonly IWikipediaIngestionService _ingestionService;
    private readonly IRagRetrievalService _retrievalService;
    private readonly IAISummarizer _summarizer;
    private readonly IGraphBuilderService _graphBuilder;
    private readonly ISessionRepository _sessionRepository;
    private readonly IVectorStore _vectorStore;

    public QueryOrchestrator(
        IWikiClient wikiClient,
        IWikipediaIngestionService ingestionService,
        IRagRetrievalService retrievalService,
        IAISummarizer summarizer,
        IGraphBuilderService graphBuilder,
        ISessionRepository sessionRepository,
        IVectorStore vectorStore)
    {
        _wikiClient = wikiClient;
        _ingestionService = ingestionService;
        _retrievalService = retrievalService;
        _summarizer = summarizer;
        _graphBuilder = graphBuilder;
        _sessionRepository = sessionRepository;
        _vectorStore = vectorStore;
    }

    public async Task<QueryResponse> ExecuteAsync(QueryRequest request, CancellationToken cancellationToken = default)
    {
        // Reuse the previous conversation when available so retrieval and summarization stay session-aware.
        var existingSession = _sessionRepository.GetSession(request.SessionId);
        var priorMessages = existingSession?.Messages ?? [];

        // Resolve the source article first because URL-only requests still need a canonical title.
        var seedPrompt = ResolveSeedPrompt(request);
        var page = _wikiClient.ResolvePage(seedPrompt, request.SourceUrl);
        var effectivePrompt = ResolveEffectivePrompt(request, page);
        var normalizedRequest = request with { Prompt = effectivePrompt };
        var nowUtc = DateTime.UtcNow;
        var sessionTitle = InferTitle(effectivePrompt);

        // Make sure the session row exists before any artifacts are written.
        _sessionRepository.EnsureSession(request.SessionId, sessionTitle, nowUtc);
        var chunks = _ingestionService.CreateChunks(page);
        await _vectorStore.UpsertAsync(request.SessionId, page, chunks, cancellationToken);

        // Pull the best supporting chunks, generate the answer, and build the graph from the same inputs.
        var retrievalPrompt = BuildRetrievalPrompt(effectivePrompt, priorMessages);
        var context = await _retrievalService.RetrieveContextAsync(request.SessionId, retrievalPrompt, 4, cancellationToken);
        var answer = await _summarizer.GenerateAsync(normalizedRequest, effectivePrompt, page, priorMessages, context, cancellationToken);
        var graphs = _graphBuilder.BuildGraphs(normalizedRequest, page, context);

        _sessionRepository.SaveQueryArtifacts(new QueryArtifacts(
            request.SessionId,
            sessionTitle,
            new MessageDto("user", BuildUserMessageContent(request, effectivePrompt), nowUtc),
            new MessageDto("assistant", answer.AssistantText, nowUtc),
            answer.Citations,
            graphs));

        return new QueryResponse(request.SessionId, answer.AssistantText, answer.Citations, graphs);
    }

    private static string ResolveSeedPrompt(QueryRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Prompt))
        {
            return request.Prompt.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.SourceUrl))
        {
            return request.SourceUrl.Trim();
        }

        return "Wikipedia topic";
    }

    private static string ResolveEffectivePrompt(QueryRequest request, WikipediaPage page)
    {
        if (!string.IsNullOrWhiteSpace(request.Prompt))
        {
            return request.Prompt.Trim();
        }

        return page.Title;
    }

    private static string BuildUserMessageContent(QueryRequest request, string effectivePrompt)
    {
        if (string.IsNullOrWhiteSpace(request.SourceUrl))
        {
            return effectivePrompt;
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return $"Wikipedia URL: {request.SourceUrl}";
        }

        return $"{effectivePrompt}{Environment.NewLine}Wikipedia URL: {request.SourceUrl}";
    }

    private static string BuildRetrievalPrompt(string effectivePrompt, IReadOnlyList<MessageDto> priorMessages)
    {
        // Keep only the most recent user turns; assistant text tends to be verbose and less useful for retrieval.
        var priorUserFocus = priorMessages
            .Where(message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
            .TakeLast(2)
            .Select(message => message.Content)
            .ToArray();

        return priorUserFocus.Length == 0
            ? effectivePrompt
            : $"{effectivePrompt} {string.Join(' ', priorUserFocus)}";
    }

    private static string InferTitle(string prompt)
    {
        var title = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(5);
        return string.Join(' ', title).Trim() is { Length: > 0 } value ? value : "New session";
    }
}
