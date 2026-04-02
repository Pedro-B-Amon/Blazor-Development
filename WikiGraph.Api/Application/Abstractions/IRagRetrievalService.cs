using WikiGraph.Api.Application.Models;

namespace WikiGraph.Api.Application.Abstractions;

/// <summary>
/// Retrieves the best matching chunks for a prompt within a session.
/// </summary>
public interface IRagRetrievalService
{
    Task<IReadOnlyList<RetrievedContext>> RetrieveContextAsync(string sessionId, string prompt, int topK = 3, CancellationToken cancellationToken = default);
}
