using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WikiGraph.Api.Configuration;
using WikiGraph.Api.Application.Services;
using WikiGraph.Api.Controllers;
using WikiGraph.Api.Infrastructure.Persistence;
using WikiGraph.Api.Infrastructure.Wikipedia;
using WikiGraph.Contracts;
using Xunit;

namespace WikiGraph.Tests;

public class ApiEndpointTests
{
    [Fact]
    public async Task QueryEndpoint_ReturnsExpectedPayload()
    {
        var controller = BuildController();
        var response = await controller.Query(new QueryRequest("session-x", "Roman architecture", null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<QueryResponse>(ok.Value);
        Assert.NotNull(payload);
        Assert.Equal("session-x", payload.SessionId);
        Assert.NotEmpty(payload.Graphs);
    }

    private static QueryController BuildController()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wikigraph-api-{Guid.NewGuid():N}.db");
        var connectionFactory = new TestConnectionFactory(path);
        var memoryDb = new SessionMemoryDb(connectionFactory);
        var repository = new SqliteSessionRepository(connectionFactory, memoryDb);
        var serviceProvider = new NullServiceProvider();
        var options = Options.Create(new OpenAIOptions());
        var vectorStore = new SqliteVectorStore(connectionFactory, memoryDb, serviceProvider, options, NullLogger<SqliteVectorStore>.Instance);
        var orchestrator = new QueryOrchestrator(
            new WikipediaApiClient(),
            new WikipediaIngestionService(),
            new RagRetrievalService(vectorStore),
            new AISummarizer(serviceProvider, options, NullLogger<AISummarizer>.Instance),
            new GraphBuilderService(),
            repository,
            vectorStore);

        return new QueryController(orchestrator);
    }

    private sealed class TestConnectionFactory(string path) : ISqliteConnectionFactory
    {
        public SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection($"Data Source={path}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();
            return connection;
        }
    }

    private sealed class NullServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
