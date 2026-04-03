using System.Text.Json;
using WikiGraph.Api.Application.Models;

namespace WikiGraph.Api.Infrastructure.Wikipedia;

public sealed class WikipediaService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WikipediaService> _logger;

    // Creates the Wikipedia client wrapper used by the API.
    public WikipediaService(HttpClient httpClient, ILogger<WikipediaService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    // Resolves a Wikipedia article, or a fallback article when the fetch fails.
    public async Task<WikiArticle> GetArticleAsync(string topic, string? wikipediaUrl, CancellationToken cancellationToken = default)
    {
        var cleanTopic = TextTools.Clean(topic);
        var cleanUrl = TextTools.Clean(wikipediaUrl);
        var requestedTitle = ResolveRequestedTitle(cleanTopic, cleanUrl);
        var resolvedTitle = await SearchBestTitleAsync(requestedTitle, cleanUrl, cancellationToken);
        var sourceUrl = ResolveUrl(resolvedTitle, cleanUrl);

        try
        {
            var page = await LoadPageAsync(resolvedTitle, includeLinks: true, cancellationToken);
            if (page is null)
            {
                return BuildFallbackArticle(resolvedTitle, cleanTopic, sourceUrl);
            }

            return await BuildArticleAsync(page.Value, cleanTopic, sourceUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Wikipedia fetch failed for {Title}; using fallback content.", resolvedTitle);
            return BuildFallbackArticle(resolvedTitle, cleanTopic, sourceUrl);
        }
    }

    // Searches for the best canonical title when the user passed free-form text.
    private async Task<string> SearchBestTitleAsync(string requestedTitle, string wikipediaUrl, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(wikipediaUrl) || string.IsNullOrWhiteSpace(requestedTitle))
        {
            return requestedTitle;
        }

        try
        {
            // MediaWiki `action=query&list=search` turns free-form text like "grass types" into a canonical title.
            using var document = await GetJsonDocumentAsync(
                $"?action=query&format=json&formatversion=2&list=search&srnamespace=0&srlimit=1&srsearch={Uri.EscapeDataString(requestedTitle)}",
                cancellationToken);

            if (!document.RootElement.TryGetProperty("query", out var query) ||
                !query.TryGetProperty("search", out var results) ||
                results.ValueKind != JsonValueKind.Array)
            {
                return requestedTitle;
            }

            foreach (var result in results.EnumerateArray())
            {
                var title = TextTools.Clean(ReadString(result, "title"));
                if (!string.IsNullOrWhiteSpace(title))
                {
                    return title;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Wikipedia title search failed for {Title}; keeping requested title.", requestedTitle);
        }

        return requestedTitle;
    }

    // Loads a single Wikipedia page payload with extracts and optional links.
    private async Task<JsonElement?> LoadPageAsync(string title, bool includeLinks, CancellationToken cancellationToken)
    {
        var props = includeLinks ? "extracts|info|links" : "extracts|info";
        var linkOptions = includeLinks ? "&plnamespace=0&pllimit=8" : string.Empty;

        // MediaWiki `action=query&prop=extracts|info|links` provides the article summary, URL, and outbound links.
        using var document = await GetJsonDocumentAsync(
            $"?action=query&format=json&formatversion=2&redirects=1&prop={props}&inprop=url&explaintext=1&exintro=1&titles={Uri.EscapeDataString(title)}{linkOptions}",
            cancellationToken);

        if (!document.RootElement.TryGetProperty("query", out var query) ||
            !query.TryGetProperty("pages", out var pages) ||
            pages.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var page in pages.EnumerateArray())
        {
            if (!page.TryGetProperty("missing", out _))
            {
                // Clone the page element because the backing JsonDocument is disposed when this method returns.
                return page.Clone();
            }
        }

        return null;
    }

    // Builds the article model from the fetched Wikipedia page payload.
    private async Task<WikiArticle> BuildArticleAsync(
        JsonElement page,
        string originalTopic,
        string fallbackUrl,
        CancellationToken cancellationToken)
    {
        var title = ReadString(page, "title") ?? "Wikipedia topic";
        var sourceUrl = ReadString(page, "fullurl") ?? fallbackUrl;
        var extract = TextTools.Clean(ReadString(page, "extract"));
        var summary = string.IsNullOrWhiteSpace(extract)
            ? $"Wikipedia article for {title}."
            : FirstSentence(extract);

        var article = new WikiArticle(title, sourceUrl)
        {
            Summary = summary,
            RetrievedUtc = DateTime.UtcNow
        };

        // Keep the article model small and UI-friendly: overview, a few details, then related-topic summaries.
        AddSections(article, extract);

        var linkedTitles = ReadLinkedTitles(page, title);
        var relatedTopics = await LoadRelatedTopicsAsync(linkedTitles, cancellationToken);
        if (relatedTopics.Count == 0)
        {
            relatedTopics = BuildSeedRelatedTopics(title, originalTopic);
        }

        foreach (var relatedTopic in relatedTopics)
        {
            article.RelatedArticles.Add(relatedTopic.Title);
            article.RelatedTopicDetails.Add(relatedTopic);
            article.Sections.Add(new WikiSection($"Related Topic: {relatedTopic.Title}", relatedTopic.Summary));
        }

        article.Sections.Add(new WikiSection(
            "Related Topics",
            string.Join(" ", article.RelatedTopicDetails.Take(4).Select(topic => $"{topic.Title}: {topic.Summary}"))));

        return article;
    }

    // Loads related topic summaries for the linked article titles.
    private async Task<IReadOnlyList<WikiTopicReference>> LoadRelatedTopicsAsync(
        IReadOnlyList<string> linkedTitles,
        CancellationToken cancellationToken)
    {
        if (linkedTitles.Count == 0)
        {
            return [];
        }

        try
        {
            var titleList = string.Join('|', linkedTitles.Select(title => title.Replace('|', ' ')));
            // MediaWiki `action=query` also accepts multiple titles, which keeps related-topic lookup to one request.
            using var document = await GetJsonDocumentAsync(
                $"?action=query&format=json&formatversion=2&redirects=1&prop=extracts|info&inprop=url&explaintext=1&exintro=1&titles={Uri.EscapeDataString(titleList)}",
                cancellationToken);

            if (!document.RootElement.TryGetProperty("query", out var query) ||
                !query.TryGetProperty("pages", out var pages) ||
                pages.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var topics = new List<WikiTopicReference>();
            foreach (var page in pages.EnumerateArray())
            {
                var title = TextTools.Clean(ReadString(page, "title"));
                var extract = TextTools.Clean(ReadString(page, "extract"));
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(extract))
                {
                    continue;
                }

                topics.Add(new WikiTopicReference(
                    title,
                    ReadString(page, "fullurl") ?? ResolveUrl(title, null),
                    FirstSentence(extract)));
            }

            return topics
                .DistinctBy(topic => topic.Title, StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Wikipedia related-topic lookup failed.");
            return [];
        }
    }

    // Fetches raw JSON from Wikipedia and parses it into a document.
    private async Task<JsonDocument> GetJsonDocumentAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(relativeUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
    }

    // Adds overview, detail, and key-point sections to the article model.
    private static void AddSections(WikiArticle article, string extract)
    {
        article.Sections.Add(new WikiSection("Overview", article.Summary));

        foreach (var paragraph in SplitParagraphs(extract).Take(2))
        {
            if (!string.Equals(paragraph, article.Summary, StringComparison.OrdinalIgnoreCase))
            {
                article.Sections.Add(new WikiSection("Details", paragraph));
            }
        }

        foreach (var sentence in SplitSentences(extract).Skip(1).Take(3))
        {
            article.Sections.Add(new WikiSection("Key Point", sentence));
        }
    }

    // Reads and filters linked article titles from the page payload.
    private static IReadOnlyList<string> ReadLinkedTitles(JsonElement page, string articleTitle)
    {
        if (!page.TryGetProperty("links", out var links) || links.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return links.EnumerateArray()
            .Select(link => TextTools.Clean(ReadString(link, "title")))
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Where(title => !title.Contains(':', StringComparison.Ordinal))
            .Where(title => !string.Equals(title, articleTitle, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
    }

    // Builds fallback related topics from the current title and prompt.
    private static IReadOnlyList<WikiTopicReference> BuildSeedRelatedTopics(string title, string originalTopic)
    {
        var seedTitles = TextTools.ExtractTerms(originalTopic, 4)
            .Select(Capitalize)
            .Concat([$"{title} history", $"{title} applications", $"{title} materials"])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4);

        return seedTitles
            .Select(seedTitle => new WikiTopicReference(
                seedTitle,
                ResolveUrl(seedTitle, null),
                $"{seedTitle} is a related Wikipedia topic that adds context around {title}."))
            .ToArray();
    }

    // Builds a fallback article when Wikipedia cannot be fetched or parsed.
    private static WikiArticle BuildFallbackArticle(string title, string topic, string wikipediaUrl)
    {
        var article = new WikiArticle(title, wikipediaUrl)
        {
            Summary = $"{title} is treated here as a Wikipedia research topic. Start with the main idea, note its definitions and uses, and compare it with nearby related topics for context.",
            RetrievedUtc = DateTime.UtcNow
        };

        article.Sections.Add(new WikiSection("Overview", article.Summary));
        article.Sections.Add(new WikiSection(
            "Details",
            $"Use {title} as the center topic, then branch into history, structure, applications, and related pages for a broader picture."));

        foreach (var relatedTopic in BuildSeedRelatedTopics(title, topic))
        {
            article.RelatedArticles.Add(relatedTopic.Title);
            article.RelatedTopicDetails.Add(relatedTopic);
        }

        article.Sections.Add(new WikiSection(
            "Related Topics",
            string.Join(" ", article.RelatedTopicDetails.Select(topicInfo => $"{topicInfo.Title}: {topicInfo.Summary}"))));

        return article;
    }

    // Resolves a Wikipedia title from a URL path segment or the original topic.
    private static string ResolveRequestedTitle(string topic, string? wikipediaUrl)
    {
        if (!string.IsNullOrWhiteSpace(wikipediaUrl) && Uri.TryCreate(wikipediaUrl, UriKind.Absolute, out var uri))
        {
            var segment = uri.Segments.LastOrDefault()?.Trim('/');
            if (!string.IsNullOrWhiteSpace(segment))
            {
                return Uri.UnescapeDataString(segment.Replace('_', ' '));
            }
        }

        return string.IsNullOrWhiteSpace(topic) ? "Wikipedia topic" : topic;
    }

    // Resolves the final article URL, or builds one from the title.
    private static string ResolveUrl(string title, string? wikipediaUrl)
    {
        if (!string.IsNullOrWhiteSpace(wikipediaUrl))
        {
            return wikipediaUrl;
        }

        return $"https://en.wikipedia.org/wiki/{Uri.EscapeDataString(title.Replace(' ', '_'))}";
    }

    // Reads a string property from a JSON element when the property exists.
    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    // Returns the first sentence, or a trimmed fallback when none exists.
    private static string FirstSentence(string text)
    {
        var sentence = SplitSentences(text).FirstOrDefault();
        return string.IsNullOrWhiteSpace(sentence) ? TextTools.TrimToLength(text, 280) : sentence;
    }

    // Splits text into cleaned paragraph-sized chunks.
    private static IEnumerable<string> SplitParagraphs(string text)
    {
        return text
            .Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(TextTools.Clean)
            .Where(value => !string.IsNullOrWhiteSpace(value));
    }

    // Splits text into cleaned sentences with trailing punctuation restored.
    private static IEnumerable<string> SplitSentences(string text)
    {
        return text
            .Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(TextTools.Clean)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.EndsWith(".", StringComparison.Ordinal) ? value : $"{value}.");
    }

    // Capitalizes the first letter of a word for display.
    private static string Capitalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Topic";
        }

        return char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
    }
}
