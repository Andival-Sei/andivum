using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.InteropServices;
using Windows.Security.Credentials;

namespace Andivum_Windows.Auth;

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
    string? Scope);

public sealed class TokenStore
{
    private const string Resource = "Andivum";
    private const string UserName = "current-session";

    public void Save(TokenSet tokenSet)
    {
        var vault = new PasswordVault();
        RemoveExisting(vault);
        vault.Add(new PasswordCredential(
            Resource,
            UserName,
            JsonSerializer.Serialize(tokenSet)));
    }

    public TokenSet? Read()
    {
        try
        {
            var credential = new PasswordVault().Retrieve(Resource, UserName);
            credential.RetrievePassword();
            return JsonSerializer.Deserialize<TokenSet>(credential.Password);
        }
        catch (COMException)
        {
            return null;
        }
    }

    public void Clear()
    {
        RemoveExisting(new PasswordVault());
    }

    private static void RemoveExisting(PasswordVault vault)
    {
        try
        {
            var credential = vault.Retrieve(Resource, UserName);
            vault.Remove(credential);
        }
        catch (COMException)
        {
            // No saved session is the normal first-run state.
        }
    }
}
