using WikiGraph.Contracts;

namespace WikiGraph.Api.Application.Models;

/// <summary>
/// Bundles the user message, assistant message, citations, and graphs created for one query.
/// </summary>
public sealed record QueryArtifacts(
    string SessionId,
    string SessionTitle,
    MessageDto UserMessage,
    MessageDto AssistantMessage,
    IReadOnlyList<CitationDto> Citations,
    IReadOnlyList<GraphDto> Graphs);

/// <summary>
/// The AI-generated text plus the citations that support it.
/// </summary>
public sealed record GeneratedAnswer(string AssistantText, IReadOnlyList<CitationDto> Citations);
