using WikiGraph.Api.Application.Models;

namespace WikiGraph.Api.Infrastructure.Persistence;

public interface IVectorStore
{
    Task UpsertAsync(string sessionId, WikipediaPage page, IReadOnlyList<WikipediaChunk> chunks, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RetrievedContext>> SearchAsync(string sessionId, string prompt, int count, CancellationToken cancellationToken = default);
}
