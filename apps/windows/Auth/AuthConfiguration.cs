namespace Andivum_Windows.Auth;

public enum AuthProvider
{
    Supabase,
}

public sealed record AuthConfiguration(
    AuthProvider Provider,
    string SupabaseUrl,
    string SupabasePublishableKey)
{
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
            .ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(provider) && provider != "supabase")
        {
            throw new InvalidOperationException(
                "Unsupported ANDIVUM_AUTH_PROVIDER. Only 'supabase' is available.");
        }

        return new AuthConfiguration(
            AuthProvider.Supabase,
            NormalizeSupabaseUrl(
                getEnvironmentVariable("ANDIVUM_SUPABASE_URL")
                    ?? "http://localhost:54321",
                "ANDIVUM_SUPABASE_URL"),
            getEnvironmentVariable("ANDIVUM_SUPABASE_PUBLISHABLE_KEY")
                ?.Trim()
                .TakeIfNotBlank()
                ?? "local-publishable-key");
    }

    public static AuthConfiguration? FromLaunchArguments(
        string? arguments,
        Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(arguments))
        {
            return null;
        }

        var launchValues = new Dictionary<string, string?>();
        foreach (var argument in arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (argument.StartsWith("--andivum-auth-provider=", StringComparison.OrdinalIgnoreCase))
            {
                launchValues["ANDIVUM_AUTH_PROVIDER"] = argument[
                    "--andivum-auth-provider=".Length..];
            }
            else if (argument.StartsWith(
                         "--andivum-supabase-url=",
                         StringComparison.OrdinalIgnoreCase))
            {
                launchValues["ANDIVUM_SUPABASE_URL"] = argument[
                    "--andivum-supabase-url=".Length..];
            }
            else if (argument.StartsWith(
                         "--andivum-supabase-publishable-key=",
                         StringComparison.OrdinalIgnoreCase))
            {
                launchValues["ANDIVUM_SUPABASE_PUBLISHABLE_KEY"] = argument[
                    "--andivum-supabase-publishable-key=".Length..];
            }
        }

        if (launchValues.Count == 0)
        {
            return null;
        }

        return FromEnvironment(
            "windows",
            key => launchValues.TryGetValue(key, out var value)
                ? value
                : getEnvironmentVariable(key));
    }

    public static AuthConfiguration? FromProcessArguments(
        IReadOnlyList<string> arguments,
        Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        return FromLaunchArguments(
            string.Join(' ', arguments.Skip(1)),
            getEnvironmentVariable);
    }

    private static string NormalizeSupabaseUrl(string value, string key)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)) ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             uri.Host is not ("localhost" or "127.0.0.1" or "10.0.2.2")))
        {
            throw new InvalidOperationException(
                $"{key} must be an HTTPS origin without a path; HTTP is allowed only for local development.");
        }

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }
}

internal static class StringExtensions
{
    public static string? TakeIfNotBlank(this string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
