namespace Andivum_Windows.Auth;

public enum AuthProvider
{
    Local,
    Auth0Supabase,
}

public sealed record AuthConfiguration(
    AuthProvider Provider,
    string Issuer,
    string ClientId,
    string RedirectUri,
    string? SupabaseUrl = null,
    string? SupabasePublishableKey = null)
{
    public bool UsesSupabase => Provider == AuthProvider.Auth0Supabase;

    public static AuthConfiguration FromEnvironment(
        string platform,
        Func<string, string?> getEnvironmentVariable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        var normalizedPlatform = platform.Trim().ToLowerInvariant();
        if (normalizedPlatform is not ("windows" or "android"))
        {
            throw new ArgumentException(
                $"Unsupported native auth platform '{platform}'.",
                nameof(platform));
        }

        var provider = getEnvironmentVariable("ANDIVUM_AUTH_PROVIDER")
            ?.Trim()
            .ToLowerInvariant() switch
        {
            null or "" or "local" => AuthProvider.Local,
            "auth0-supabase" => AuthProvider.Auth0Supabase,
            var value => throw new InvalidOperationException(
                $"Unsupported ANDIVUM_AUTH_PROVIDER '{value}'. Use 'local' or 'auth0-supabase'."),
        };

        return provider switch
        {
            AuthProvider.Local => new AuthConfiguration(
                AuthProvider.Local,
                getEnvironmentVariable("ANDIVUM_LOCAL_AUTH_ISSUER")
                    ?? "https://localhost:7240",
                getEnvironmentVariable("ANDIVUM_LOCAL_AUTH_CLIENT_ID")
                    ?? $"andivum-{normalizedPlatform}",
                getEnvironmentVariable("ANDIVUM_LOCAL_AUTH_REDIRECT_URI")
                    ?? $"andivum://{normalizedPlatform}/auth/callback"),
            AuthProvider.Auth0Supabase => CreateAuth0Configuration(
                normalizedPlatform,
                getEnvironmentVariable),
            _ => throw new InvalidOperationException("Unsupported auth provider."),
        };
    }

    private static AuthConfiguration CreateAuth0Configuration(
        string platform,
        Func<string, string?> getEnvironmentVariable)
    {
        var domain = Required(getEnvironmentVariable, "ANDIVUM_AUTH0_DOMAIN")
            .Trim()
            .TrimEnd('/');
        if (domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            domain = domain["https://".Length..].TrimEnd('/');
        }

        if (domain.Contains("/", StringComparison.Ordinal) ||
            domain.Contains("?", StringComparison.Ordinal) ||
            domain.Contains("#", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "ANDIVUM_AUTH0_DOMAIN must contain only the Auth0 host name.");
        }

        var platformName = platform.ToUpperInvariant();
        var clientId = Required(
            getEnvironmentVariable,
            $"ANDIVUM_AUTH0_{platformName}_CLIENT_ID");
        var redirectUri = Required(
            getEnvironmentVariable,
            $"ANDIVUM_AUTH0_{platformName}_REDIRECT_URI");
        var supabaseUrl = NormalizeHttpsUrl(
            Required(getEnvironmentVariable, "ANDIVUM_SUPABASE_URL"),
            "ANDIVUM_SUPABASE_URL");
        var publishableKey = Required(
            getEnvironmentVariable,
            "ANDIVUM_SUPABASE_PUBLISHABLE_KEY");

        return new AuthConfiguration(
            AuthProvider.Auth0Supabase,
            $"https://{domain}",
            clientId,
            redirectUri,
            supabaseUrl,
            publishableKey);
    }

    private static string Required(
        Func<string, string?> getEnvironmentVariable,
        string key)
    {
        var value = getEnvironmentVariable(key)?.Trim();
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"Auth configuration is incomplete: {key} is required.")
            : value;
    }

    private static string NormalizeHttpsUrl(string value, string key)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException(
                $"{key} must be an HTTPS origin without a path.");
        }

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }
}
