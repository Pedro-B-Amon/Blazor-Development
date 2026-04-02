namespace WikiGraph.Api.Configuration;

public sealed class OpenAIOptions
{
    public string? ApiKey { get; set; }
    public string? OrganizationId { get; set; }
    public string ChatModelId { get; set; } = "gpt-4.1-mini";
    public string EmbeddingModelId { get; set; } = "text-embedding-3-small";
    public int? EmbeddingDimensions { get; set; }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(ApiKey);
}
