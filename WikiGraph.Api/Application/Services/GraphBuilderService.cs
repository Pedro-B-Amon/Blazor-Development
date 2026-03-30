using WikiGraph.Api.Application.Abstractions;
using WikiGraph.Api.Application.Models;
using WikiGraph.Contracts;

namespace WikiGraph.Api.Application.Services;

public sealed class GraphBuilderService : IGraphBuilderService
{
    public IReadOnlyList<GraphDto> BuildGraphs(QueryRequest request, WikipediaPage page, IReadOnlyList<RetrievedContext> context)
    {
        var relatedLabels = page.RelatedTopics
            .Concat(context.Select(item => item.Section))
            .Where(label => !string.Equals(label, request.Prompt, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();

        var nodes = new List<GraphNodeDto> { new("topic", request.Prompt, 5) };
        var edges = new List<GraphEdgeDto>();
        var relations = new[] { "expands", "connects", "grounds" };

        for (var index = 0; index < relatedLabels.Length; index++)
        {
            var id = $"node-{index + 1}";
            nodes.Add(new GraphNodeDto(id, relatedLabels[index], Math.Max(2, 4 - index)));
            edges.Add(new GraphEdgeDto("topic", id, relations[index % relations.Length]));
        }

        if (nodes.Count == 1)
        {
            nodes.Add(new GraphNodeDto("node-1", $"{page.Title} overview", 4));
            nodes.Add(new GraphNodeDto("node-2", "Linked articles", 3));
            nodes.Add(new GraphNodeDto("node-3", "References", 2));
            edges.Add(new GraphEdgeDto("topic", "node-1", "frames"));
            edges.Add(new GraphEdgeDto("topic", "node-2", "expands"));
            edges.Add(new GraphEdgeDto("topic", "node-3", "cites"));
        }

        return [new GraphDto(request.Prompt, nodes, edges)];
    }
}
