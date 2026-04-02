namespace WikiGraph.Api.Application.Models;

public sealed record StoredEmbedding(
    string EncodingKind,
    string? ModelId,
    int? Dimensions,
    byte[] Payload);
