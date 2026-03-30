using WikiGraph.Api.Application.Models;
using WikiGraph.Contracts;

namespace WikiGraph.Api.Application.Abstractions;

public interface IAISummarizer
{
    GeneratedAnswer Generate(QueryRequest request, WikipediaPage page, IReadOnlyList<RetrievedContext> context);
}
