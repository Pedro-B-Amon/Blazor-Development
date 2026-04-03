namespace WikiGraph.Contracts;

public sealed record SessionSummary(string SessionId, string Title, DateTime CreatedUtc, DateTime LastAccessUtc);

public sealed record SessionDetailDto(
    SessionSummary Session,
    IReadOnlyList<MessageDto> Messages,
    IReadOnlyList<CitationDto> Citations,
    IReadOnlyList<GraphDto> Graphs);

public sealed record MessageDto(string Role, string Content, DateTime CreatedUtc);

public sealed record CitationDto(string Title, string Url, string? Section = null, string? ChunkId = null);

public sealed record GraphNodeDto(string Id, string Label, int Weight);

public sealed record GraphEdgeDto(string Source, string Target, string Relation);

public sealed record GraphDto(string Topic, IReadOnlyList<GraphNodeDto> Nodes, IReadOnlyList<GraphEdgeDto> Edges);

public sealed record CreateSessionRequest(string? Title);

public sealed record AddWikiArticleRequest(string Topic, string? WikipediaUrl);
