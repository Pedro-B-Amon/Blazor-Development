using WikiGraph.Api.Application.Abstractions;
using WikiGraph.Api.Application.Models;
using WikiGraph.Contracts;

namespace WikiGraph.Api.Application.Services;

/// <summary>
/// Builds a small, readable topic graph that mirrors the article and the retrieved context.
/// </summary>
public sealed class GraphBuilderService : IGraphBuilderService
{
    public IReadOnlyList<GraphDto> BuildGraphs(QueryRequest request, WikipediaPage page, IReadOnlyList<RetrievedContext> context)
    {
        // The first topic is the request itself; the remaining topics come from the article and the retrieved sections.
        var graphTopics = new[] { request.Prompt }
            .Concat(page.RelatedTopics)
            .Concat(context.Select(item => $"{page.Title} {item.Section}"))
            .Where(topic => !string.IsNullOrWhiteSpace(topic))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();

        return graphTopics
            .Select((topic, index) => BuildGraph(topic, page, context, index == 0))
            .ToArray();
    }

    private static GraphDto BuildGraph(string topic, WikipediaPage page, IReadOnlyList<RetrievedContext> context, bool includeArticleAnchor)
    {
        // Favor labels that are already visible in the article or retrieved context so the graph feels grounded.
        var relatedLabels = page.RelatedTopics
            .Concat(context.Select(item => item.Section))
            .Concat(includeArticleAnchor ? [page.Title] : [])
            .Where(label => !string.Equals(label, topic, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();

        var nodes = new List<GraphNodeDto> { new("topic", topic, 5) };
        var edges = new List<GraphEdgeDto>();
        var relations = new[] { "expands", "connects", "grounds", "cites" };

        for (var index = 0; index < relatedLabels.Length; index++)
        {
            var id = $"node-{index + 1}";
            nodes.Add(new GraphNodeDto(id, relatedLabels[index], Math.Max(2, 4 - Math.Min(index, 2))));
            edges.Add(new GraphEdgeDto("topic", id, relations[index % relations.Length]));
        }

        if (nodes.Count == 1)
        {
            nodes.Add(new GraphNodeDto("node-1", $"{page.Title} overview", 4));
            nodes.Add(new GraphNodeDto("node-2", "Linked articles", 3));
            edges.Add(new GraphEdgeDto("topic", "node-1", "frames"));
            edges.Add(new GraphEdgeDto("topic", "node-2", "expands"));
        }

        return new GraphDto(topic, nodes, edges);
    }
}
