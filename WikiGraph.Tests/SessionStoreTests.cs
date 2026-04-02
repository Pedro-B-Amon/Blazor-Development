using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WikiGraph.Api.Application.Services;
using WikiGraph.Api.Configuration;
using WikiGraph.Api.Infrastructure.Persistence;
using WikiGraph.Api.Infrastructure.Wikipedia;
using WikiGraph.Contracts;
using Xunit;

namespace WikiGraph.Tests;

public class SessionStoreTests
{
    [Fact]
    public async Task AddArticle_PersistsSessionArtifacts()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wikigraph-tests-{Guid.NewGuid():N}.db");
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

        var session = await sessionService.AddArticleAsync("sess-1", new AddWikiArticleRequest("Climate adaptation", null));
        var reopenedMemoryDb = new SessionMemoryDb(new TestConnectionFactory(path));
        var reopenedRepository = new SqliteSessionRepository(new TestConnectionFactory(path), reopenedMemoryDb);
        var persisted = reopenedRepository.GetSession("sess-1");

        Assert.Equal("sess-1", session.Session.SessionId);
        Assert.Equal(2, session.Messages.Count);
        Assert.NotEmpty(session.Citations);
        Assert.NotEmpty(session.Graphs);
        Assert.NotNull(persisted);
        Assert.Equal(2, persisted!.Messages.Count);
        Assert.NotEmpty(persisted.Citations);
        Assert.NotEmpty(persisted.Graphs);
    }

    [Fact]
    public void CreateSession_ReturnsSortedSessions()
    {
        var connectionFactory = new TestConnectionFactory(Path.Combine(Path.GetTempPath(), $"wikigraph-tests-{Guid.NewGuid():N}.db"));
        var repository = new SqliteSessionRepository(connectionFactory, new SessionMemoryDb(connectionFactory));

        repository.CreateSession("First");
        Thread.Sleep(20);
        repository.CreateSession("Second");

        var sessions = repository.GetSessions();

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
}
