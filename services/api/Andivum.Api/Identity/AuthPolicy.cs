using System.Text;

namespace Andivum.Api.Identity;

public static class AuthPolicy
{
    public const int MaxPasskeyDisplayNameLength = 64;
    public const int MaxActivePasskeysPerUser = 20;

    public static bool IsPasskeyDisplayNameAllowed(string? displayName)
    {
        return !string.IsNullOrWhiteSpace(displayName) &&
            displayName.EnumerateRunes().Count() <= MaxPasskeyDisplayNameLength;
    }

    public static bool CanAddPasskey(int activePasskeyCount)
    {
        return activePasskeyCount >= 0 &&
            activePasskeyCount < MaxActivePasskeysPerUser;
    }

    public static bool CanAuthorizeWithPasskey(int activePasskeyCount)
    {
        return activePasskeyCount > 0;
    }

    public static bool IsAllowedOrigin(
        string? origin,
        IEnumerable<string> allowedOrigins)
    {
        return origin is not null &&
            Uri.TryCreate(origin, UriKind.Absolute, out var parsedOrigin) &&
            string.Equals(parsedOrigin.Scheme, Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase) &&
            allowedOrigins.Contains(origin, StringComparer.Ordinal);
    }

    public static bool IsPublicClientCredential(string? clientSecret)
    {
        return string.IsNullOrEmpty(clientSecret);
    }

    public static bool IsS256PkceRequest(
        string? codeChallenge,
        string? codeChallengeMethod)
    {
        return !string.IsNullOrWhiteSpace(codeChallenge) &&
            string.Equals(codeChallengeMethod, "S256", StringComparison.Ordinal);
    }
}
