using Microsoft.Data.Sqlite;
using WikiGraph.Api;
using WikiGraph.Contracts;
using Xunit;

namespace WikiGraph.Tests;

public class SessionStoreTests
{
    [Fact]
    public void AppendQuery_PersistsSessionArtifacts()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wikigraph-tests-{Guid.NewGuid():N}.db");
        var store = new SqliteSessionStore(new TestConnectionFactory(path));

        var response = store.AppendQuery(new QueryRequest("sess-1", "Climate adaptation", null));
        var session = store.GetSession("sess-1");
        var reopenedStore = new SqliteSessionStore(new TestConnectionFactory(path));
        var persisted = reopenedStore.GetSession("sess-1");

        Assert.Equal("sess-1", response.SessionId);
        Assert.NotEmpty(response.AssistantText);
        Assert.Single(response.Graphs);
        Assert.Equal(3, response.Citations.Count);
        Assert.NotNull(session);
        Assert.Equal(2, session!.Messages.Count);
        Assert.Equal("Climate adaptation", session.Session.Title);
        Assert.NotNull(persisted);
        Assert.Equal(2, persisted!.Messages.Count);
        Assert.Single(persisted.Graphs);
        Assert.Equal(3, persisted.Citations.Count);
    }

    [Fact]
    public void CreateSession_ReturnsSortedSessions()
    {
        var store = new SqliteSessionStore(new TestConnectionFactory(Path.Combine(Path.GetTempPath(), $"wikigraph-tests-{Guid.NewGuid():N}.db")));

        store.CreateSession("First");
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
            return connection;
        }
    }
}
