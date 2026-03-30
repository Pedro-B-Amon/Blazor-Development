using WikiGraph.Api.Application.Models;
using WikiGraph.Contracts;

namespace WikiGraph.Api.Application.Abstractions;

public interface IGraphBuilderService
{
    IReadOnlyList<GraphDto> BuildGraphs(QueryRequest request, WikipediaPage page, IReadOnlyList<RetrievedContext> context);
}
