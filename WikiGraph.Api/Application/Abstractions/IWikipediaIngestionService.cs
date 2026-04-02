using WikiGraph.Api.Application.Models;

namespace WikiGraph.Api.Application.Abstractions;

/// <summary>
/// Converts a Wikipedia page into deterministic storage chunks.
/// </summary>
public interface IWikipediaIngestionService
{
    IReadOnlyList<WikipediaChunk> CreateChunks(WikipediaPage page);
}
