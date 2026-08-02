using System.Text.Json.Serialization;

namespace Andivum_Windows.Auth;

public interface ITokenStore
{
    TokenSet? Read();

    void Save(TokenSet tokenSet);

    void Clear();
}

public sealed record TokenSet(
    [property: JsonPropertyName("access_token")]
    string AccessToken,
    [property: JsonPropertyName("refresh_token")]
    string? RefreshToken,
    [property: JsonPropertyName("token_type")]
    string TokenType,
    [property: JsonPropertyName("expires_in")]
    int ExpiresIn,
    [property: JsonPropertyName("scope")]
    string? Scope,
    [property: JsonPropertyName("issued_at")]
    DateTimeOffset IssuedAt = default,
    [property: JsonPropertyName("id_token")]
    string? IdToken = null);
