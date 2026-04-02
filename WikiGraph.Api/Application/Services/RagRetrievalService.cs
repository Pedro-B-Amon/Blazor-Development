using WikiGraph.Api.Application.Abstractions;
using WikiGraph.Api.Application.Models;
using WikiGraph.Api.Infrastructure.Persistence;

namespace WikiGraph.Api.Application.Services;

/// <summary>
/// Thin adapter over the vector store so the orchestrator does not need to know storage details.
/// </summary>
public sealed class RagRetrievalService : IRagRetrievalService
{
    private readonly IVectorStore _vectorStore;

    public RagRetrievalService(IVectorStore vectorStore)
    {
        _vectorStore = vectorStore;
    }

    // This stays intentionally thin so the retrieval strategy remains centralized in the vector store.
    public Task<IReadOnlyList<RetrievedContext>> RetrieveContextAsync(string sessionId, string prompt, int topK = 3, CancellationToken cancellationToken = default) =>
        _vectorStore.SearchAsync(sessionId, prompt, topK, cancellationToken);
}
