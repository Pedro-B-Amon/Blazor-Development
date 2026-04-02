using WikiGraph.Contracts;

namespace WikiGraph.Api.Application.Models;

public sealed record QueryArtifacts(
    string SessionId,
    string SessionTitle,
    MessageDto UserMessage,
    MessageDto AssistantMessage,
    IReadOnlyList<CitationDto> Citations,
    IReadOnlyList<GraphDto> Graphs);

public sealed record GeneratedAnswer(string AssistantText, IReadOnlyList<CitationDto> Citations);
