using Andivum.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Andivum.Api.Tests;

public sealed class DatabaseSchemaTests
{
    [Fact]
    public async Task Postgres_schema_contains_identity_and_openiddict_tables()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__Postgres");

        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "ConnectionStrings__Postgres must be set for the PostgreSQL integration test.");

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:Postgres", connectionString);
            });

        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        await database.Database.MigrateAsync();

        var tableNames = await database.Database
            .SqlQuery<string>($"""
                SELECT table_name AS "Value"
                FROM information_schema.tables
                WHERE table_schema = 'public'
                """)
            .ToListAsync();

        Assert.Contains("AspNetUsers", tableNames);
        Assert.Contains("AspNetUserPasskeys", tableNames);
        Assert.Contains("OpenIddictApplications", tableNames);
        Assert.Contains("OpenIddictTokens", tableNames);
    }
}
