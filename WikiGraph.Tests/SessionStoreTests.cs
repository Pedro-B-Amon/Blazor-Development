using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WikiGraph.Api;
using WikiGraph.Api.Configuration;
using WikiGraph.Api.Application.Services;
using WikiGraph.Api.Infrastructure.Persistence;
using WikiGraph.Api.Infrastructure.Wikipedia;
using WikiGraph.Contracts;
using Xunit;

namespace WikiGraph.Tests;

public class SessionStoreTests
{
    [Fact]
    public async Task AppendQuery_PersistsSessionArtifacts()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wikigraph-tests-{Guid.NewGuid():N}.db");
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

        var response = await orchestrator.ExecuteAsync(new QueryRequest("sess-1", "Climate adaptation", null));
        var session = repository.GetSession("sess-1");
        var reopenedMemoryDb = new SessionMemoryDb(new TestConnectionFactory(path));
        var reopenedStore = new SqliteSessionRepository(new TestConnectionFactory(path), reopenedMemoryDb);
        var persisted = reopenedStore.GetSession("sess-1");

        Assert.Equal("sess-1", response.SessionId);
        Assert.NotEmpty(response.AssistantText);
        Assert.NotEmpty(response.Graphs);
        Assert.Equal(3, response.Citations.Count);
        Assert.NotNull(session);
        Assert.Equal(2, session!.Messages.Count);
        Assert.Equal("Climate adaptation", session.Session.Title);
        Assert.NotNull(persisted);
        Assert.Equal(2, persisted!.Messages.Count);
        Assert.NotEmpty(persisted.Graphs);
        Assert.Equal(3, persisted.Citations.Count);
    }

    [Fact]
    public void CreateSession_ReturnsSortedSessions()
    {
        var connectionFactory = new TestConnectionFactory(Path.Combine(Path.GetTempPath(), $"wikigraph-tests-{Guid.NewGuid():N}.db"));
        var store = new SqliteSessionRepository(connectionFactory, new SessionMemoryDb(connectionFactory));

        store.CreateSession("First");
        Thread.Sleep(20);
        store.CreateSession("Second");

        var sessions = store.GetSessions();

        Assert.Equal(2, sessions.Count);
        Assert.Equal("Second", sessions[0].Title);
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
