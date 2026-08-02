using Andivum.Api.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Andivum.Api.Tests;

public sealed class AuthPolicyTests
{
    [Fact]
    public async Task Development_passkey_options_pin_localhost_and_explicit_origin()
    {
        var options = new IdentityPasskeyOptions();

        IdentityOptionsSetup.Configure(
            options,
            environmentName: "Development",
            serverDomain: null,
            allowedOrigins: ["https://localhost:7240"]);

        Assert.Equal("localhost", options.ServerDomain);
        Assert.Equal("required", options.UserVerificationRequirement);
        Assert.True(await options.ValidateOrigin!(new PasskeyOriginValidationContext
        {
            HttpContext = new DefaultHttpContext(),
            Origin = "https://localhost:7240",
            CrossOrigin = false,
        }));
        Assert.False(await options.ValidateOrigin!(new PasskeyOriginValidationContext
        {
            HttpContext = new DefaultHttpContext(),
            Origin = "https://evil.example",
            CrossOrigin = false,
        }));
        Assert.False(await options.ValidateOrigin!(new PasskeyOriginValidationContext
        {
            HttpContext = new DefaultHttpContext(),
            Origin = "http://localhost:7240",
            CrossOrigin = false,
        }));
    }

    [Fact]
    public void Production_passkey_options_require_an_explicit_relying_party_id()
    {
        var options = new IdentityPasskeyOptions();

        Assert.Throws<InvalidOperationException>(() =>
            IdentityOptionsSetup.Configure(
                options,
                environmentName: "Production",
                serverDomain: null,
                allowedOrigins: ["https://app.example"]));
    }

    [Fact]
    public void Production_host_fails_before_serving_requests_without_a_relying_party_id()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.UseSetting(
                    "ConnectionStrings:Postgres",
                    "Host=localhost;Port=5433;Database=andivum_test;Username=andivum_test;Password=andivum_test_local_only");
                builder.UseSetting("Database:AutoMigrate", "false");
            });

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("ServerDomain", exception.ToString());
    }

    [Fact]
    public void Passkey_display_name_uses_a_64_unicode_scalar_limit()
    {
        Assert.True(AuthPolicy.IsPasskeyDisplayNameAllowed(new string('a', 64)));
        Assert.False(AuthPolicy.IsPasskeyDisplayNameAllowed(new string('a', 65)));
        Assert.True(AuthPolicy.IsPasskeyDisplayNameAllowed(
            string.Concat(Enumerable.Repeat("😀", 64))));
        Assert.False(AuthPolicy.IsPasskeyDisplayNameAllowed(
            string.Concat(Enumerable.Repeat("😀", 65))));
    }

    [Fact]
    public void Passkey_count_is_capped_at_20_active_credentials()
    {
        Assert.True(AuthPolicy.CanAddPasskey(0));
        Assert.True(AuthPolicy.CanAddPasskey(19));
        Assert.False(AuthPolicy.CanAddPasskey(20));
    }

    [Fact]
    public void Native_redirects_are_exact_and_client_secrets_are_rejected()
    {
        var registry = NativeClientRegistry.CreateDevelopment();

        Assert.True(registry.IsAllowedRedirect(
            "andivum-windows",
            "andivum://windows/auth/callback"));
        Assert.False(registry.IsAllowedRedirect(
            "andivum-windows",
            "andivum://windows/auth/callback/"));
        Assert.False(registry.IsAllowedRedirect(
            "andivum-windows",
            "https://evil.example/callback"));
        Assert.True(AuthPolicy.IsPublicClientCredential(null));
        Assert.False(AuthPolicy.IsPublicClientCredential("unexpected-secret"));
    }

    [Fact]
    public void Native_authorization_requires_s256_pkce()
    {
        Assert.True(AuthPolicy.IsS256PkceRequest("challenge", "S256"));
        Assert.False(AuthPolicy.IsS256PkceRequest("challenge", "plain"));
        Assert.False(AuthPolicy.IsS256PkceRequest("", "S256"));
        Assert.False(AuthPolicy.IsS256PkceRequest(null, "S256"));
    }
}
