using WikiGraph.Api.Application.Abstractions;
using WikiGraph.Api.Application.Models;
using WikiGraph.Api.Infrastructure.Persistence;
using WikiGraph.Api.Infrastructure.Wikipedia;
using WikiGraph.Contracts;

namespace WikiGraph.Api.Application.Services;

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

    public QueryResponse Execute(QueryRequest request)
    {
        var page = _wikiClient.ResolvePage(request.Prompt, request.SourceUrl);
        var chunks = _ingestionService.CreateChunks(page);
        _vectorStore.Upsert(request.SessionId, page, chunks);

        var context = _retrievalService.RetrieveContext(request.SessionId, request.Prompt, 3);
        var answer = _summarizer.Generate(request, page, context);
        var graphs = _graphBuilder.BuildGraphs(request, page, context);

        var nowUtc = DateTime.UtcNow;
        _sessionRepository.SaveQueryArtifacts(new QueryArtifacts(
            request.SessionId,
            InferTitle(request.Prompt),
            new MessageDto("user", request.Prompt, nowUtc),
            new MessageDto("assistant", answer.AssistantText, nowUtc),
            answer.Citations,
            graphs));

        return new QueryResponse(request.SessionId, answer.AssistantText, answer.Citations, graphs);
    }

    private static string InferTitle(string prompt)
    {
        var title = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(5);
        return string.Join(' ', title).Trim() is { Length: > 0 } value ? value : "New session";
    }
}
