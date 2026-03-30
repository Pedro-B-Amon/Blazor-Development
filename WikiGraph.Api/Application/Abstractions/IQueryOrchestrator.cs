using WikiGraph.Contracts;

namespace WikiGraph.Api.Application.Abstractions;

public interface IQueryOrchestrator
{
    Task<QueryResponse> ExecuteAsync(QueryRequest request, CancellationToken cancellationToken = default);
}
