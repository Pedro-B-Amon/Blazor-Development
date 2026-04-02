using WikiGraph.Api.Application.Abstractions;
using WikiGraph.Api.Application.Models;
using WikiGraph.Api.Infrastructure.Persistence;

namespace WikiGraph.Api.Application.Services;

public sealed class RagRetrievalService : IRagRetrievalService
{
    private readonly IVectorStore _vectorStore;

    public RagRetrievalService(IVectorStore vectorStore)
    {
        _vectorStore = vectorStore;
    }

    public Task<IReadOnlyList<RetrievedContext>> RetrieveContextAsync(string sessionId, string prompt, int topK = 3, CancellationToken cancellationToken = default) =>
        _vectorStore.SearchAsync(sessionId, prompt, topK, cancellationToken);
}
