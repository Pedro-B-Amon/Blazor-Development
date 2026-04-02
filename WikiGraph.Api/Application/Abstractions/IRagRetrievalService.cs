using WikiGraph.Api.Application.Models;

namespace WikiGraph.Api.Application.Abstractions;

public interface IRagRetrievalService
{
    Task<IReadOnlyList<RetrievedContext>> RetrieveContextAsync(string sessionId, string prompt, int topK = 3, CancellationToken cancellationToken = default);
}
