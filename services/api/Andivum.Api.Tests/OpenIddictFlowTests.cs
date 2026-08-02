using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Andivum.Api.Tests;

public sealed class OpenIddictFlowTests
{
    [Fact]
    public async Task Discovery_document_is_available()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        using var response = await client.GetAsync(
            "/.well-known/openid-configuration");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("authorization_endpoint", body);
        Assert.Contains("token_endpoint", body);
    }

    [Fact]
    public async Task Session_endpoint_rejects_anonymous_requests()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        using var response = await client.GetAsync("/api/v1/session");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authorization_rejects_an_unknown_redirect_uri()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        using var response = await client.GetAsync(
            "/connect/authorize?client_id=andivum-windows" +
            "&response_type=code" +
            "&redirect_uri=https%3A%2F%2Fevil.example%2Fcallback" +
            "&scope=openid%20profile" +
            "&code_challenge=challenge" +
            "&code_challenge_method=S256");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Authorization_rejects_requests_without_pkce()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        using var response = await client.GetAsync(
            "/connect/authorize?client_id=andivum-windows" +
            "&response_type=code" +
            "&redirect_uri=andivum%3A%2F%2Fwindows%2Fauth%2Fcallback" +
            "&scope=openid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Registered_native_client_reaches_the_passkey_surface()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        using var response = await client.GetAsync(
            "/connect/authorize?client_id=andivum-windows" +
            "&response_type=code" +
            "&redirect_uri=andivum%3A%2F%2Fwindows%2Fauth%2Fcallback" +
            "&scope=openid" +
            "&code_challenge=challenge" +
            "&code_challenge_method=S256");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        Assert.Contains("Continue with passkey", body);
    }

    [Fact]
    public async Task Anonymous_passkey_creation_options_are_rejected()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        using var response = await client.PostAsJsonAsync(
            "/Account/PasskeyCreationOptions",
            new { displayName = "Windows Hello" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Passkey_request_options_do_not_require_a_username()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        using var response = await client.PostAsync(
            "/Account/PasskeyRequestOptions",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "challenge",
            await response.Content.ReadAsStringAsync());
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(
                    "ConnectionStrings:Postgres",
                    Environment.GetEnvironmentVariable(
                        "ConnectionStrings__Postgres") ??
                    throw new InvalidOperationException(
                        "ConnectionStrings__Postgres must be set by the dedicated test runner."));
            });
    }

    private static HttpClient CreateHttpsClient(WebApplicationFactory<Program> factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });
    }
}
