using Microsoft.Data.Sqlite;

namespace WikiGraph.Api.Infrastructure.Persistence;

/// <summary>
/// Opens SQLite connections with the project's configured database path and safety pragmas.
/// </summary>
public interface ISqliteConnectionFactory
{
    // Opens a connection using the configured SQLite database.
    SqliteConnection OpenConnection();
}

/// <summary>
/// Default file-backed SQLite connection factory used by the API and tests.
/// </summary>
public sealed class SqliteConnectionFactory : ISqliteConnectionFactory
{
    private readonly string _connectionString;

    // Reads the connection string from configuration or falls back to the local database file.
    public SqliteConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("WikiGraph") ?? "Data Source=wikigraph.db";
    }

    // Opens a connection and enables foreign keys on it.
    public SqliteConnection OpenConnection()
    {
        // SQLite keeps foreign keys disabled by default, so every connection enables them explicitly.
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        connection.ExecuteNonQuery("PRAGMA foreign_keys = ON;");
        return connection;
    }
}
