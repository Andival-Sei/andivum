using Npgsql;

namespace Andivum.Api.Identity;

public static class DevelopmentDatabasePolicy
{
    public static bool IsLocalDevelopmentConnection(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var host = builder.Host?.Trim().ToLowerInvariant();
            var database = builder.Database;

            return (host is "localhost" or "127.0.0.1" or "::1") &&
                database is not null &&
                database.StartsWith("andivum_", StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
