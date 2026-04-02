using System.Globalization;
using System.Text.RegularExpressions;

namespace WikiGraph.Api.Application.Models;

public sealed record WikipediaSection(string Heading, string Content);

public sealed record WikipediaPage(
    string PageId,
    string Title,
    string SourceUrl,
    DateTime RetrievedUtc,
    IReadOnlyList<WikipediaSection> Sections,
    IReadOnlyList<string> RelatedTopics);

public sealed record WikipediaChunk(
    string ChunkId,
    string PageId,
    string Section,
    string Text,
    string Hash,
    IReadOnlyList<string> Terms);

public sealed record RetrievedContext(
    string ChunkId,
    string Section,
    string Text,
    string SourceUrl,
    double Score);

internal static partial class TextTokenizer
{
    private static readonly TextInfo TextInfo = CultureInfo.InvariantCulture.TextInfo;

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

    public static string Slugify(string value)
    {
        var slug = string.Join('-', ExtractTerms(value));
        return string.IsNullOrWhiteSpace(slug) ? "item" : slug;
    }

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
