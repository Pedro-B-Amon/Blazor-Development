using WikiGraph.Api.Application.Models;

namespace WikiGraph.Api.Infrastructure.Wikipedia;

public sealed class WikipediaApiClient : IWikiClient
{
    public WikipediaPage ResolvePage(string prompt, string? sourceUrl)
    {
        var title = ResolveTitle(prompt, sourceUrl);
        var canonicalUrl = ResolveUrl(title, sourceUrl);
        var seedTerms = TextTokenizer.ExtractTerms(prompt, 4);
        var relatedTopics = seedTerms
            .Select(term => $"{TextTokenizer.ToTitleCase(term)} context")
            .Concat(new[]
            {
                $"{title} history",
                $"{title} applications",
                $"{title} references"
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();

        WikipediaSection[] sections =
        [
            new WikipediaSection(
                "Overview",
                $"{title} is treated here as a Wikipedia-first research topic. Start from the main article summary, capture the defining ideas, and identify the key subtopics linked from the overview."),
            new WikipediaSection(
                "History",
                $"Historical reading helps explain how {title.ToLowerInvariant()} evolved, which people, events, or discoveries shaped it, and which timeline markers deserve follow-up reading."),
            new WikipediaSection(
                "Study Angles",
                $"Use study angles such as {string.Join(", ", seedTerms.Select(TextTokenizer.ToTitleCase).DefaultIfEmpty("scope review"))} to branch into related pages, compare sections, and collect citations for a note-ready outline."),
            new WikipediaSection(
                "References",
                $"Finish by checking references, see-also links, and category pages connected to {title} so the topic graph grows from verified Wikipedia navigation paths.")
        ];

        return new WikipediaPage(
            TextTokenizer.Slugify(title),
            title,
            canonicalUrl,
            DateTime.UtcNow,
            sections,
            relatedTopics);
    }

    private static string ResolveTitle(string prompt, string? sourceUrl)
    {
        if (!string.IsNullOrWhiteSpace(sourceUrl) && Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri))
        {
            var lastSegment = uri.Segments.LastOrDefault()?.Trim('/');
            if (!string.IsNullOrWhiteSpace(lastSegment))
            {
                return Uri.UnescapeDataString(lastSegment.Replace('_', ' '));
            }
        }

        if (!string.IsNullOrWhiteSpace(prompt))
        {
            return prompt.Trim();
        }

        if (!string.IsNullOrWhiteSpace(sourceUrl))
        {
            return sourceUrl.Trim();
        }

        return "Wikipedia topic";
    }

    private static string ResolveUrl(string title, string? sourceUrl)
    {
        if (!string.IsNullOrWhiteSpace(sourceUrl))
        {
            return sourceUrl.Trim();
        }

        return $"https://en.wikipedia.org/wiki/{Uri.EscapeDataString(title.Replace(' ', '_'))}";
    }
}
