using System.Security.Cryptography;

namespace Andivum_Windows.Auth;

public static class Pkce
{
    public static string CreateVerifier()
    {
        return Base64Url(RandomNumberGenerator.GetBytes(32));
    }

    public static string CreateChallenge(string verifier)
    {
        return Base64Url(SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier)));
    }

    public static string CreateState()
    {
        return Base64Url(RandomNumberGenerator.GetBytes(24));
    }

    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
