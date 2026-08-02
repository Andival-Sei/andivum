using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Andivum_Windows.Auth;

public sealed record SessionResponse(
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("authenticated")] bool Authenticated);

public sealed class ProtectedSessionClient
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromSeconds(30);

    private readonly HttpClient httpClient;
    private readonly ITokenStore tokenStore;
    private readonly string clientId;
    private readonly Uri tokenEndpoint;
    private readonly Uri sessionEndpoint;
    private readonly Func<DateTimeOffset> clock;

    public ProtectedSessionClient(
        HttpClient httpClient,
        ITokenStore tokenStore,
        string clientId,
        Uri tokenEndpoint,
        Uri sessionEndpoint,
        Func<DateTimeOffset>? clock = null)
    {
        this.httpClient = httpClient;
        this.tokenStore = tokenStore;
        this.clientId = clientId;
        this.tokenEndpoint = tokenEndpoint;
        this.sessionEndpoint = sessionEndpoint;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<SessionResponse> GetCurrentSessionAsync(
        CancellationToken cancellationToken = default)
    {
        var token = tokenStore.Read() ??
            throw new InvalidOperationException("No saved session is available.");
        var hasRefreshed = false;

        if (NeedsRefresh(token))
        {
            token = await RefreshAsync(token, cancellationToken);
            hasRefreshed = true;
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                sessionEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue(
                string.IsNullOrWhiteSpace(token.TokenType)
                    ? "Bearer"
                    : token.TokenType,
                token.AccessToken);

            using var response = await httpClient.SendAsync(
                request,
                cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized &&
                !hasRefreshed &&
                !string.IsNullOrWhiteSpace(token.RefreshToken))
            {
                token = await RefreshAsync(token, cancellationToken);
                hasRefreshed = true;
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Session request failed with HTTP {(int)response.StatusCode}.");
            }

            var session = await response.Content.ReadFromJsonAsync<SessionResponse>(
                cancellationToken: cancellationToken);
            return session ?? throw new InvalidOperationException(
                "Session endpoint returned no session data.");
        }

        throw new InvalidOperationException("Session validation could not be completed.");
    }

    private bool NeedsRefresh(TokenSet token)
    {
        if (string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            return false;
        }

        if (token.IssuedAt == default)
        {
            return true;
        }

        var refreshAt = token.IssuedAt +
            TimeSpan.FromSeconds(
                Math.Max(0, token.ExpiresIn - RefreshSkew.TotalSeconds));
        return clock() >= refreshAt;
    }

    private async Task<TokenSet> RefreshAsync(
        TokenSet current,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsync(
            tokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = current.RefreshToken ?? string.Empty,
            }),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Session refresh failed with HTTP {(int)response.StatusCode}.");
        }

        var refreshed = await response.Content.ReadFromJsonAsync<TokenSet>(
            cancellationToken: cancellationToken);
        if (refreshed is null || string.IsNullOrWhiteSpace(refreshed.AccessToken))
        {
            throw new InvalidOperationException(
                "Session refresh returned no access token.");
        }

        var token = refreshed with
        {
            RefreshToken = string.IsNullOrWhiteSpace(refreshed.RefreshToken)
                ? current.RefreshToken
                : refreshed.RefreshToken,
            IdToken = string.IsNullOrWhiteSpace(refreshed.IdToken)
                ? current.IdToken
                : refreshed.IdToken,
            IssuedAt = clock(),
        };
        tokenStore.Save(token);
        return token;
    }
}
