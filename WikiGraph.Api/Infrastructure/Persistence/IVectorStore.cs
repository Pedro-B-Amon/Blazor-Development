using WikiGraph.Api.Application.Models;

namespace WikiGraph.Api.Infrastructure.Persistence;

public interface IVectorStore
{
    void Upsert(string sessionId, WikipediaPage page, IReadOnlyList<WikipediaChunk> chunks);
    IReadOnlyList<RetrievedContext> Search(string sessionId, string prompt, int count);
}
