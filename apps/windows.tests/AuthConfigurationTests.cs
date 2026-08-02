using Andivum_Windows.Auth;
using Xunit;

namespace Andivum_Windows.Tests;

public sealed class AuthConfigurationTests
{
    [Fact]
    public void Local_defaults_keep_the_explicit_development_fallback()
    {
        var configuration = AuthConfiguration.FromEnvironment(
            "windows",
            _ => null);

        Assert.Equal(AuthProvider.Local, configuration.Provider);
        Assert.Equal("https://localhost:7240", configuration.Issuer);
        Assert.Equal("andivum-windows", configuration.ClientId);
        Assert.Equal("andivum://windows/auth/callback", configuration.RedirectUri);
        Assert.False(configuration.UsesSupabase);
    }

    [Fact]
    public void Auth0_configuration_uses_platform_client_and_supabase_data_settings()
    {
        var values = new Dictionary<string, string?>
        {
            ["ANDIVUM_AUTH_PROVIDER"] = "auth0-supabase",
            ["ANDIVUM_AUTH0_DOMAIN"] = "dev-example.eu.auth0.com",
            ["ANDIVUM_AUTH0_WINDOWS_CLIENT_ID"] = "windows-client",
            ["ANDIVUM_AUTH0_WINDOWS_REDIRECT_URI"] = "andivum://windows/auth/callback",
            ["ANDIVUM_SUPABASE_URL"] = "https://example.supabase.co",
            ["ANDIVUM_SUPABASE_PUBLISHABLE_KEY"] = "publishable-key",
        };

        var configuration = AuthConfiguration.FromEnvironment(
            "windows",
            key => values.GetValueOrDefault(key));

        Assert.Equal(AuthProvider.Auth0Supabase, configuration.Provider);
        Assert.Equal("https://dev-example.eu.auth0.com", configuration.Issuer);
        Assert.Equal("windows-client", configuration.ClientId);
        Assert.Equal("andivum://windows/auth/callback", configuration.RedirectUri);
        Assert.Equal("https://example.supabase.co", configuration.SupabaseUrl);
        Assert.Equal("publishable-key", configuration.SupabasePublishableKey);
        Assert.True(configuration.UsesSupabase);
    }

    [Fact]
    public void Auth0_configuration_fails_clearly_when_a_required_value_is_missing()
    {
        var values = new Dictionary<string, string?>
        {
            ["ANDIVUM_AUTH_PROVIDER"] = "auth0-supabase",
            ["ANDIVUM_AUTH0_DOMAIN"] = "dev-example.eu.auth0.com",
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AuthConfiguration.FromEnvironment(
                "windows",
                key => values.GetValueOrDefault(key)));

        Assert.Contains("ANDIVUM_AUTH0_WINDOWS_CLIENT_ID", exception.Message);
    }

    [Fact]
    public void Auth0_configuration_rejects_a_supabase_url_that_is_not_an_origin()
    {
        var values = new Dictionary<string, string?>
        {
            ["ANDIVUM_AUTH_PROVIDER"] = "auth0-supabase",
            ["ANDIVUM_AUTH0_DOMAIN"] = "dev-example.eu.auth0.com",
            ["ANDIVUM_AUTH0_WINDOWS_CLIENT_ID"] = "windows-client",
            ["ANDIVUM_AUTH0_WINDOWS_REDIRECT_URI"] = "andivum://windows/auth/callback",
            ["ANDIVUM_SUPABASE_URL"] = "https://example.supabase.co?unexpected=query",
            ["ANDIVUM_SUPABASE_PUBLISHABLE_KEY"] = "publishable-key",
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AuthConfiguration.FromEnvironment(
                "windows",
                key => values.GetValueOrDefault(key)));

        Assert.Contains("ANDIVUM_SUPABASE_URL", exception.Message);
    }
}
