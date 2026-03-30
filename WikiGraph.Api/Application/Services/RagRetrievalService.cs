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

    public IReadOnlyList<RetrievedContext> RetrieveContext(string sessionId, string prompt, int topK = 3) =>
        _vectorStore.Search(sessionId, prompt, topK);
}
