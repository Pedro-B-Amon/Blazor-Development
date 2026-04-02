using System.Globalization;
using System.Text.RegularExpressions;

namespace WikiGraph.Api.Application.Models;

/// <summary>
/// A single section extracted from a Wikipedia page.
/// </summary>
public sealed record WikipediaSection(string Heading, string Content);

/// <summary>
/// Canonical Wikipedia content and metadata used by ingestion, retrieval, and summarization.
/// </summary>
public sealed record WikipediaPage(
    string PageId,
    string Title,
    string SourceUrl,
    DateTime RetrievedUtc,
    IReadOnlyList<WikipediaSection> Sections,
    IReadOnlyList<string> RelatedTopics);

/// <summary>
/// A chunk produced from a page section and stored for later retrieval.
/// </summary>
public sealed record WikipediaChunk(
    string ChunkId,
    string PageId,
    string Section,
    string Text,
    string Hash,
    IReadOnlyList<string> Terms);

/// <summary>
/// The text snippets returned from retrieval and fed into summarization.
/// </summary>
public sealed record RetrievedContext(
    string ChunkId,
    string Section,
    string Text,
    string SourceUrl,
    double Score);

internal static partial class TextTokenizer
{
    private static readonly TextInfo TextInfo = CultureInfo.InvariantCulture.TextInfo;

    /// <summary>
    /// Breaks text into stable, lowercase terms that can be used for chunking and keyword matching.
    /// </summary>
    public static IReadOnlyList<string> ExtractTerms(string text, int maxTerms = int.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return WordRegex()
            .Matches(text.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(value => value.Length > 2)
            .Distinct(StringComparer.Ordinal)
            .Take(maxTerms)
            .ToArray();
    }

    /// <summary>
    /// Creates a predictable slug from a heading or topic label.
    /// </summary>
    public static string Slugify(string value)
    {
        var slug = string.Join('-', ExtractTerms(value));
        return string.IsNullOrWhiteSpace(slug) ? "item" : slug;
    }

    /// <summary>
    /// Normalizes a label into title case for display.
    /// </summary>
    public static string ToTitleCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Topic";
        }

        return TextInfo.ToTitleCase(value.ToLowerInvariant());
    }

    [GeneratedRegex("[a-z0-9]+", RegexOptions.Compiled)]
    private static partial Regex WordRegex();
}
