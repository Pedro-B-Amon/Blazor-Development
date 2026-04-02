using Microsoft.Data.Sqlite;

namespace WikiGraph.Api.Infrastructure.Persistence;

internal static class SqliteConnectionExtensions
{
    /// <summary>
    /// Runs a one-off SQL statement without repeating command creation boilerplate.
    /// </summary>
    public static void ExecuteNonQuery(this SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
