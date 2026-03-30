using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using WikiGraph.Api;
using WikiGraph.Contracts;
using Xunit;

namespace WikiGraph.Tests;

public class ApiEndpointTests : IClassFixture<ApiEndpointTests.SqliteApiFactory>
{
    private readonly SqliteApiFactory _factory;

    public ApiEndpointTests(SqliteApiFactory factory) => _factory = factory;

    [Fact]
    public async Task QueryEndpoint_ReturnsExpectedPayload()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/query", new QueryRequest("session-x", "Roman architecture", null));

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<QueryResponse>();

        Assert.NotNull(payload);
        Assert.Equal("session-x", payload!.SessionId);
        Assert.Single(payload.Graphs);
    }

    public sealed class SqliteApiFactory : WebApplicationFactory<ApiAssemblyMarker>
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"wikigraph-api-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<ISqliteConnectionFactory>(_ => new TestConnectionFactory(_path));
                services.AddSingleton<ISessionStore, SqliteSessionStore>();
            });
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
}
