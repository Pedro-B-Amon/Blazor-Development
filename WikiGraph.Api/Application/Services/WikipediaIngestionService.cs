using System.Security.Cryptography;
using System.Text;
using WikiGraph.Api.Application.Abstractions;
using WikiGraph.Api.Application.Models;

namespace WikiGraph.Api.Application.Services;

public sealed class WikipediaIngestionService : IWikipediaIngestionService
{
    public IReadOnlyList<WikipediaChunk> CreateChunks(WikipediaPage page)
    {
        return page.Sections
            .Select(section =>
            {
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
