namespace WikiGraph.Contracts;

public sealed record SessionSummary(string SessionId, string Title, DateTime CreatedUtc, DateTime LastAccessUtc);
// These DTOs are the stable API contracts shared by the browser UI and REST API.
public sealed record MessageDto(string Role, string Content, DateTime CreatedUtc);
public sealed record CitationDto(string Title, string Url, string? Section = null, string? ChunkId = null);
public sealed record GraphNodeDto(string Id, string Label, int Weight);
public sealed record GraphEdgeDto(string Source, string Target, string Relation);
public sealed record GraphDto(string Topic, IReadOnlyList<GraphNodeDto> Nodes, IReadOnlyList<GraphEdgeDto> Edges);
public sealed record SessionDetailDto(SessionSummary Session, IReadOnlyList<MessageDto> Messages, IReadOnlyList<CitationDto> Citations, IReadOnlyList<GraphDto> Graphs);
public sealed record QueryRequest(string SessionId, string Prompt, string? SourceUrl);
public sealed record CreateSessionRequest(string? Title);
public sealed record QueryResponse(string SessionId, string AssistantText, IReadOnlyList<CitationDto> Citations, IReadOnlyList<GraphDto> Graphs);
