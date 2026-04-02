namespace WikiGraph.Contracts;

/// <summary>
/// Minimal session metadata shared by the API and browser clients.
/// </summary>
public sealed record SessionSummary(string SessionId, string Title, DateTime CreatedUtc, DateTime LastAccessUtc);

/// <summary>
/// A full session snapshot including messages, citations, and graphs.
/// </summary>
public sealed record SessionDetailDto(SessionSummary Session, IReadOnlyList<MessageDto> Messages, IReadOnlyList<CitationDto> Citations, IReadOnlyList<GraphDto> Graphs);

/// <summary>
/// A single chat-style message stored inside a session.
/// </summary>
public sealed record MessageDto(string Role, string Content, DateTime CreatedUtc);

/// <summary>
/// A citation that points back to a Wikipedia article or article section.
/// </summary>
public sealed record CitationDto(string Title, string Url, string? Section = null, string? ChunkId = null);

/// <summary>
/// A node in the lightweight graph returned with each query response.
/// </summary>
public sealed record GraphNodeDto(string Id, string Label, int Weight);

/// <summary>
/// A directed relationship between two graph nodes.
/// </summary>
public sealed record GraphEdgeDto(string Source, string Target, string Relation);

/// <summary>
/// A small topic graph derived from the article, retrieved context, and prompt.
/// </summary>
public sealed record GraphDto(string Topic, IReadOnlyList<GraphNodeDto> Nodes, IReadOnlyList<GraphEdgeDto> Edges);

/// <summary>
/// Input used to ask a question or start a Wikipedia-backed session.
/// </summary>
public sealed record QueryRequest(string SessionId, string Prompt, string? SourceUrl);

/// <summary>
/// Request body for creating a session directly.
/// </summary>
public sealed record CreateSessionRequest(string? Title);

/// <summary>
/// The query response sent back to the UI.
/// </summary>
public sealed record QueryResponse(string SessionId, string AssistantText, IReadOnlyList<CitationDto> Citations, IReadOnlyList<GraphDto> Graphs);
