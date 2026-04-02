using WikiGraph.Api.Application.Models;
using WikiGraph.Contracts;

namespace WikiGraph.Api.Application.Abstractions;

/// <summary>
/// Produces an answer from the page, session history, and retrieved context.
/// </summary>
public interface IAISummarizer
{
    Task<GeneratedAnswer> GenerateAsync(
        QueryRequest request,
        string effectivePrompt,
        WikipediaPage page,
        IReadOnlyList<MessageDto> sessionHistory,
        IReadOnlyList<RetrievedContext> context,
        CancellationToken cancellationToken = default);
}
