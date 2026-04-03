using Microsoft.SemanticKernel;
using System.Net.Http.Headers;
using WikiGraph.Api.Application.Services;
using WikiGraph.Api.Infrastructure.Persistence;
using WikiGraph.Api.Infrastructure.Wikipedia;

namespace WikiGraph.Api.Configuration;

public static class ServiceCollectionExtensions
{
    // Registers the API services, persistence, HTTP client, and optional Gemini integrations.
    public static IServiceCollection AddWikiGraphApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddOpenApi();
        services.Configure<GeminiOptions>(options =>
        {
            configuration.GetSection("Gemini").Bind(options);
            options.ApiKey = string.IsNullOrWhiteSpace(options.ApiKey)
                ? Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? string.Empty
                : options.ApiKey;
        });

        services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<SessionMemoryDb>();
        services.AddSingleton<SqliteSessionRepository>();
        services.AddHttpClient<WikipediaService>((_, client) =>
        {
            var contactEmail = Environment.GetEnvironmentVariable("WIKIGRAPH_CONTACT_EMAIL")?.Trim() ?? string.Empty;
            var userAgent = string.IsNullOrWhiteSpace(contactEmail)
                ? "WikiGraph/1.0"
                : $"WikiGraph/1.0 ({contactEmail})";

            // Wikipedia access here is just the public MediaWiki JSON API at /w/api.php.
            client.BaseAddress = new Uri("https://en.wikipedia.org/w/api.php");
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });
        services.AddSingleton<SqliteVectorStore>();
        services.AddTransient<GeminiService>();
        services.AddTransient<WikiSessionService>();

        var geminiOptions = ResolveGeminiOptions(configuration);
        if (geminiOptions.IsEnabled)
        {
#pragma warning disable SKEXP0070
            services.AddGoogleAIGeminiChatCompletion(
                geminiOptions.TextModel,
                geminiOptions.ApiKey,
                serviceId: "wikigraph-chat");
            services.AddGoogleAIEmbeddingGenerator(
                geminiOptions.EmbeddingModel,
                geminiOptions.ApiKey,
                serviceId: "wikigraph-embeddings",
                dimensions: geminiOptions.EmbeddingDimensions);
#pragma warning restore SKEXP0070
        }

        return services;
    }

    // Reads Gemini settings from config and environment variables.
    private static GeminiOptions ResolveGeminiOptions(IConfiguration configuration)
    {
        var options = configuration.GetSection("Gemini").Get<GeminiOptions>() ?? new GeminiOptions();
        options.ApiKey = string.IsNullOrWhiteSpace(options.ApiKey)
            ? Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? string.Empty
            : options.ApiKey;
        return options;
    }
}
