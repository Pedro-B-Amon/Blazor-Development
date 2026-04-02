using System.Security.Cryptography;
using System.Text;
using WikiGraph.Api.Application.Abstractions;
using WikiGraph.Api.Application.Models;

namespace WikiGraph.Api.Application.Services;

/// <summary>
/// Turns each Wikipedia section into a deterministic, deduplicated chunk for retrieval and storage.
/// </summary>
public sealed class WikipediaIngestionService : IWikipediaIngestionService
{
    public IReadOnlyList<WikipediaChunk> CreateChunks(WikipediaPage page)
    {
        return page.Sections
            .Select(section =>
            {
                // Keep the chunk body compact and repeat the page title so the chunk still reads well in isolation.
                var chunkText = $"{page.Title}: {section.Content}";
                return new WikipediaChunk(
                    $"{page.PageId}:{TextTokenizer.Slugify(section.Heading)}",
                    page.PageId,
                    section.Heading,
                    chunkText,
                    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(chunkText))),
                    TextTokenizer.ExtractTerms($"{page.Title} {section.Heading} {section.Content}"));
            })
            .ToArray();
    }
}
