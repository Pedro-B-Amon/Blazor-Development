using WikiGraph.Api.Application.Models;

namespace WikiGraph.Api.Infrastructure.Persistence;

/// <summary>
/// Stores chunks and retrieves ranked context for a query prompt.
/// </summary>
public interface IVectorStore
{
    Task UpsertAsync(string sessionId, WikipediaPage page, IReadOnlyList<WikipediaChunk> chunks, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RetrievedContext>> SearchAsync(string sessionId, string prompt, int count, CancellationToken cancellationToken = default);
}
