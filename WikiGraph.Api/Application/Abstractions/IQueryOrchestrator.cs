using WikiGraph.Contracts;

namespace WikiGraph.Api.Application.Abstractions;

/// <summary>
/// Runs the end-to-end query pipeline and returns the final API response.
/// </summary>
public interface IQueryOrchestrator
{
    Task<QueryResponse> ExecuteAsync(QueryRequest request, CancellationToken cancellationToken = default);
}
