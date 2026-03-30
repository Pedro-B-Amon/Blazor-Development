using WikiGraph.Api.Application.Abstractions;
using WikiGraph.Api.Application.Models;
using WikiGraph.Contracts;

namespace WikiGraph.Api.Application.Services;

public sealed class AISummarizer : IAISummarizer
{
    public GeneratedAnswer Generate(QueryRequest request, WikipediaPage page, IReadOnlyList<RetrievedContext> context)
    {
        var selectedContext = context.Take(3).ToArray();
        var concepts = selectedContext.Length > 0
            ? string.Join(", ", selectedContext.Select(item => item.Section.ToLowerInvariant()))
            : "overview, history, and related concepts";

        var notes = selectedContext.Length > 0
            ? selectedContext.Select((item, index) => $"{index + 1}. {item.Section}: {BuildSentence(item.Text)}")
            : ["1. Overview: Start with the main article summary and expand into linked sections."];

        var assistantText = $"""
            Study guide for {request.Prompt}

            Core concepts: {concepts}.
            Research focus: anchor your notes on {page.Title}, compare the highlighted sections, and branch into linked topics.

            Key notes:
            {string.Join(Environment.NewLine, notes)}

            Next step: follow the citations below to expand the graph with narrower Wikipedia subtopics.

            Source anchor: {page.SourceUrl}
            """;

        var citations = selectedContext
            .Select(item => new CitationDto(
                item.Section.Equals("Overview", StringComparison.OrdinalIgnoreCase) ? page.Title : $"{page.Title} {item.Section}",
                item.Section.Equals("Overview", StringComparison.OrdinalIgnoreCase) ? page.SourceUrl : $"{page.SourceUrl}#{TextTokenizer.Slugify(item.Section)}",
                item.Section))
            .Distinct()
            .Take(3)
            .ToArray();

        if (citations.Length == 0)
        {
            citations =
            [
                new CitationDto(page.Title, page.SourceUrl),
                new CitationDto($"{page.Title} overview", page.SourceUrl, "Overview"),
                new CitationDto($"{page.Title} references", $"{page.SourceUrl}#references", "References")
            ];
        }

        return new GeneratedAnswer(assistantText, citations);
    }

    private static string BuildSentence(string text)
    {
        var normalized = text.ReplaceLineEndings(" ").Trim();
        if (normalized.Length <= 110)
        {
            return normalized;
        }

        return $"{normalized[..107].TrimEnd()}...";
    }
}
