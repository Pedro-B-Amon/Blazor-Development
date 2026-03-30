using Microsoft.Data.Sqlite;

namespace WikiGraph.Api.Infrastructure.Persistence;

public interface ISqliteConnectionFactory
{
    SqliteConnection OpenConnection();
}

public sealed class SqliteConnectionFactory : ISqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("WikiGraph") ?? "Data Source=wikigraph.db";
    }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        connection.ExecuteNonQuery("PRAGMA foreign_keys = ON;");
        return connection;
    }
}
