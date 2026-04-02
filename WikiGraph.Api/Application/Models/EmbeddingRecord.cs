namespace WikiGraph.Api.Application.Models;

/// <summary>
/// Encodes how a chunk embedding was produced so the store can fall back gracefully.
/// </summary>
public sealed record StoredEmbedding(
    string EncodingKind,
    string? ModelId,
    int? Dimensions,
    byte[] Payload);
