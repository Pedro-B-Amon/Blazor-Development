namespace WikiGraph.Api.Configuration;

public sealed class GeminiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string TextModel { get; set; } = "gemini-2.5-flash";
    public string EmbeddingModel { get; set; } = "gemini-embedding-001";
    public int? EmbeddingDimensions { get; set; }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(ApiKey);
}
