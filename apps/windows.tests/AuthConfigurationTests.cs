using Andivum_Windows.Auth;
using Xunit;

namespace Andivum_Windows.Tests;

public sealed class AuthConfigurationTests
{
    [Fact]
    public void Defaults_use_the_local_supabase_endpoint_for_development()
    {
        var configuration = AuthConfiguration.FromEnvironment(
            "windows",
            _ => null);

        Assert.Equal(AuthProvider.Supabase, configuration.Provider);
        Assert.Equal("http://localhost:54321", configuration.SupabaseUrl);
        Assert.Equal("local-publishable-key", configuration.SupabasePublishableKey);
    }

    [Fact]
    public void Supabase_configuration_uses_the_cloud_url_and_publishable_key()
    {
        var values = new Dictionary<string, string?>
        {
            ["ANDIVUM_AUTH_PROVIDER"] = "supabase",
            ["ANDIVUM_SUPABASE_URL"] = "https://example.supabase.co/",
            ["ANDIVUM_SUPABASE_PUBLISHABLE_KEY"] = "publishable-key",
        };

        var configuration = AuthConfiguration.FromEnvironment(
            "windows",
            key => values.GetValueOrDefault(key));

        Assert.Equal(AuthProvider.Supabase, configuration.Provider);
        Assert.Equal("https://example.supabase.co", configuration.SupabaseUrl);
        Assert.Equal("publishable-key", configuration.SupabasePublishableKey);
    }

    [Fact]
    public void Packaged_launch_arguments_override_missing_process_environment()
    {
        var configuration = AuthConfiguration.FromLaunchArguments(
            "--andivum-auth-provider=supabase " +
            "--andivum-supabase-url=https://example.supabase.co " +
            "--andivum-supabase-publishable-key=publishable-key",
            _ => null);

        Assert.NotNull(configuration);
        Assert.Equal(AuthProvider.Supabase, configuration.Provider);
        Assert.Equal("https://example.supabase.co", configuration.SupabaseUrl);
        Assert.Equal("publishable-key", configuration.SupabasePublishableKey);
    }

    [Fact]
    public void Auth0_configuration_is_rejected_after_the_migration()
    {
        var values = new Dictionary<string, string?>
        {
            ["ANDIVUM_AUTH_PROVIDER"] = "auth0-supabase",
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AuthConfiguration.FromEnvironment(
                "windows",
                key => values.GetValueOrDefault(key)));

        Assert.Contains("supabase", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Auth0", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Supabase_configuration_rejects_a_url_that_is_not_an_origin()
    {
        var values = new Dictionary<string, string?>
        {
            ["ANDIVUM_AUTH_PROVIDER"] = "supabase",
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
