using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.System;

namespace Andivum_Windows.Auth;

public sealed class WindowsAuthClient
{
    public const string ApiBaseUrl = "https://localhost:7240";
    public const string ClientId = "andivum-windows";
    public const string RedirectUri = "andivum://windows/auth/callback";

    private readonly HttpClient httpClient;
    private readonly ITokenStore tokenStore;
    private readonly Func<DateTimeOffset> clock;
    private PendingAuthorization? pendingAuthorization;

    public WindowsAuthClient()
        : this(new HttpClient(), new TokenStore())
    {
    }

    public WindowsAuthClient(
        HttpClient httpClient,
        ITokenStore tokenStore,
        Func<DateTimeOffset>? clock = null)
    {
        this.httpClient = httpClient;
        this.tokenStore = tokenStore;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public TokenSet? CurrentSession => tokenStore.Read();

    public async Task BeginSignInAsync()
    {
        var discovery = await GetDiscoveryAsync();
        var verifier = Pkce.CreateVerifier();
        var state = Pkce.CreateState();
        pendingAuthorization = new PendingAuthorization(
            state,
            verifier,
            discovery.TokenEndpoint);

        var authorizeUrl = string.Join("&", new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = RedirectUri,
            ["scope"] = "openid profile offline_access",
            ["state"] = state,
            ["code_challenge"] = Pkce.CreateChallenge(verifier),
            ["code_challenge_method"] = "S256",
        }.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

        if (!await Launcher.LaunchUriAsync(new Uri($"{discovery.AuthorizationEndpoint}?{authorizeUrl}")))
        {
            pendingAuthorization = null;
            throw new InvalidOperationException("The system browser could not be opened.");
        }
    }

    public async Task<bool> HandleCallbackAsync(Uri callbackUri)
    {
        if (callbackUri.Scheme != "andivum" ||
            callbackUri.Host != "windows" ||
            callbackUri.AbsolutePath != "/auth/callback")
        {
            return false;
        }

        var query = ParseQuery(callbackUri.Query);
        if (query.TryGetValue("error", out var error))
        {
            throw new InvalidOperationException($"Authorization failed: {error}");
        }

        if (pendingAuthorization is null ||
            !query.TryGetValue("state", out var state) ||
            !string.Equals(state, pendingAuthorization.State, StringComparison.Ordinal) ||
            !query.TryGetValue("code", out var code))
        {
            throw new InvalidOperationException("The authorization callback did not match the pending request.");
        }

        using var response = await httpClient.PostAsync(
            pendingAuthorization.TokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = RedirectUri,
                ["code_verifier"] = pendingAuthorization.Verifier,
            }));
        var responseBody = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Token exchange failed with HTTP {(int)response.StatusCode}.");
        }

        var token = JsonSerializer.Deserialize<TokenSet>(responseBody) ??
            throw new InvalidOperationException("Token endpoint returned no token set.");
        tokenStore.Save(token with { IssuedAt = clock() });
        pendingAuthorization = null;
        return true;
    }

    public async Task<SessionResponse> GetCurrentSessionAsync(
        CancellationToken cancellationToken = default)
    {
        var discovery = await GetDiscoveryAsync();
        var client = new ProtectedSessionClient(
            httpClient,
            tokenStore,
            ClientId,
            new Uri(discovery.TokenEndpoint),
            new Uri($"{ApiBaseUrl}/api/v1/session"),
            clock);
        return await client.GetCurrentSessionAsync(cancellationToken);
    }

    public void SignOut()
    {
        pendingAuthorization = null;
        tokenStore.Clear();
    }

    private async Task<DiscoveryDocument> GetDiscoveryAsync()
    {
        return await httpClient.GetFromJsonAsync<DiscoveryDocument>(
            $"{ApiBaseUrl}/.well-known/openid-configuration") ??
            throw new InvalidOperationException("OIDC discovery returned no document.");
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        return query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(pair => pair.Length == 2)
            .ToDictionary(
                pair => WebUtility.UrlDecode(pair[0]),
                pair => WebUtility.UrlDecode(pair[1]),
                StringComparer.Ordinal);
    }

    private sealed record PendingAuthorization(
        string State,
        string Verifier,
        string TokenEndpoint);

    private sealed record DiscoveryDocument(
        [property: JsonPropertyName("authorization_endpoint")] string AuthorizationEndpoint,
        [property: JsonPropertyName("token_endpoint")] string TokenEndpoint);
}
