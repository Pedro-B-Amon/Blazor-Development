using WikiGraph.Api.Application.Abstractions;
using WikiGraph.Api.Application.Services;
using WikiGraph.Api.Infrastructure.Persistence;
using WikiGraph.Api.Infrastructure.Wikipedia;

namespace WikiGraph.Api.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWikiGraphApi(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddOpenApi();

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

        return services;
    }
}
