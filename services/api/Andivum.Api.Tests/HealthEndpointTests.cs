using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace Andivum.Api.Tests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Health_endpoint_returns_ok_status()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(
                    "ConnectionStrings:Postgres",
                    "Host=localhost;Port=5432;Database=andivum_dev;Username=andivum_dev");
            });
        using var _client = factory.CreateClient();

        using var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "{\"status\":\"ok\"}",
            await response.Content.ReadAsStringAsync());
    }
}
