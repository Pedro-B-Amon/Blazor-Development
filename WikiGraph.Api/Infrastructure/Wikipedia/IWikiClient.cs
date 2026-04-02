using WikiGraph.Api.Application.Models;

namespace WikiGraph.Api.Infrastructure.Wikipedia;

public interface IWikiClient
{
    WikipediaPage ResolvePage(string prompt, string? sourceUrl);
}
