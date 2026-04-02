using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using WikiGraph.Api.Application.Abstractions;
using WikiGraph.Api.Application.Services;
using WikiGraph.Api.Infrastructure.Persistence;
using WikiGraph.Api.Infrastructure.Wikipedia;

namespace WikiGraph.Api.Configuration;

/// <summary>
/// Registers the API's controllers, services, persistence, and optional AI integrations.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWikiGraphApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddOpenApi();
        services.Configure<OpenAIOptions>(configuration.GetSection("OpenAI"));

        services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<SessionMemoryDb>();
        services.AddSingleton<ISessionRepository, SqliteSessionRepository>();
        services.AddSingleton<IVectorStore, SqliteVectorStore>();
        services.AddSingleton<IWikiClient, WikipediaApiClient>();
        services.AddSingleton<IWikipediaIngestionService, WikipediaIngestionService>();
        services.AddSingleton<IRagRetrievalService, RagRetrievalService>();
        services.AddSingleton<IAISummarizer, AISummarizer>();
        services.AddSingleton<IGraphBuilderService, GraphBuilderService>();
        services.AddSingleton<IQueryOrchestrator, QueryOrchestrator>();

        var openAIOptions = ResolveOpenAIOptions(configuration);
        if (openAIOptions.IsEnabled)
        {
            // Register the OpenAI clients only when an API key is present so local runs stay fully offline-capable.
            services.AddOpenAIChatCompletion(
                openAIOptions.ChatModelId,
                openAIOptions.ApiKey!,
                openAIOptions.OrganizationId,
                serviceId: "wikigraph-chat");

#pragma warning disable SKEXP0010
            services.AddOpenAIEmbeddingGenerator(
                openAIOptions.EmbeddingModelId,
                openAIOptions.ApiKey!,
                openAIOptions.OrganizationId,
                openAIOptions.EmbeddingDimensions,
                serviceId: "wikigraph-embeddings");
#pragma warning restore SKEXP0010
        }

        return services;
    }

    private static OpenAIOptions ResolveOpenAIOptions(IConfiguration configuration)
    {
        var options = configuration.GetSection("OpenAI").Get<OpenAIOptions>() ?? new OpenAIOptions();
        options.ApiKey ??= Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        options.OrganizationId ??= Environment.GetEnvironmentVariable("OPENAI_ORG_ID");
        return options;
    }
}
