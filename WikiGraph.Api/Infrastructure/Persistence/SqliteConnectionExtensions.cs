using Microsoft.Data.Sqlite;

namespace WikiGraph.Api.Infrastructure.Persistence;

internal static class SqliteConnectionExtensions
{
    // Runs a one-off SQL statement without repeating command creation boilerplate.
    public static void ExecuteNonQuery(this SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
