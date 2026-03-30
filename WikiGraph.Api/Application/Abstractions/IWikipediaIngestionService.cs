using WikiGraph.Api.Application.Models;

namespace WikiGraph.Api.Application.Abstractions;

public interface IWikipediaIngestionService
{
    IReadOnlyList<WikipediaChunk> CreateChunks(WikipediaPage page);
}
