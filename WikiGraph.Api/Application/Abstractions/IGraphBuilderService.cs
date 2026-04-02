using WikiGraph.Api.Application.Models;
using WikiGraph.Contracts;

namespace WikiGraph.Api.Application.Abstractions;

/// <summary>
/// Builds the lightweight graph that accompanies each query response.
/// </summary>
public interface IGraphBuilderService
{
    IReadOnlyList<GraphDto> BuildGraphs(QueryRequest request, WikipediaPage page, IReadOnlyList<RetrievedContext> context);
}
