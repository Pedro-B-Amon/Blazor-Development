using WikiGraph.Contracts;

namespace WikiGraph.Api.Application.Abstractions;

public interface IQueryOrchestrator
{
    QueryResponse Execute(QueryRequest request);
}
