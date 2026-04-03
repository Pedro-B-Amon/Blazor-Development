using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WikiGraph.Api.Application.Services;
using WikiGraph.Api.Configuration;
using WikiGraph.Api.Controllers;
using WikiGraph.Api.Infrastructure.Persistence;
using WikiGraph.Api.Infrastructure.Wikipedia;
using WikiGraph.Contracts;
using Xunit;

namespace WikiGraph.Tests;

public class ApiEndpointTests
{
    [Fact]
    public async Task AddArticleEndpoint_ReturnsUpdatedSession()
    {
        var controller = BuildController();
        var response = await controller.AddArticle("session-x", new AddWikiArticleRequest("Roman architecture", null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<SessionDetailDto>(ok.Value);
        Assert.Equal("session-x", payload.Session.SessionId);
        Assert.NotEmpty(payload.Messages);
        Assert.NotEmpty(payload.Graphs);
    }

    [Fact]
    public async Task GetGraphsEndpoint_ReturnsSessionGraphs()
    {
        var controller = BuildController();
        await controller.AddArticle("session-x", new AddWikiArticleRequest("Roman architecture", null), CancellationToken.None);

        var response = controller.GetGraphs("session-x");

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<GraphDto>>(ok.Value);
        Assert.NotEmpty(payload);
    }

    private static SessionController BuildController()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wikigraph-api-{Guid.NewGuid():N}.db");
        var connectionFactory = new TestConnectionFactory(path);
        var memoryDb = new SessionMemoryDb(connectionFactory);
        var repository = new SqliteSessionRepository(connectionFactory, memoryDb);
        var geminiService = new GeminiService(Options.Create(new GeminiOptions()), NullLogger<GeminiService>.Instance);
        var vectorStore = new SqliteVectorStore(connectionFactory, memoryDb, geminiService, NullLogger<SqliteVectorStore>.Instance);
        var sessionService = new WikiSessionService(
            new WikipediaService(new HttpClient(), NullLogger<WikipediaService>.Instance),
            geminiService,
            repository,
            vectorStore);

        return new SessionController(repository, sessionService);
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
}
