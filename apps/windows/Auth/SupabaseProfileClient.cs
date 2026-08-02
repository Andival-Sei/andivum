using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace Andivum_Windows.Auth;

public sealed class SupabaseProfileClient
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromSeconds(30);

    private readonly HttpClient httpClient;
    private readonly ITokenStore tokenStore;
    private readonly string clientId;
    private readonly Uri tokenEndpoint;
    private readonly Uri profileEndpoint;
    private readonly string publishableKey;
    private readonly Func<DateTimeOffset> clock;

    public SupabaseProfileClient(
        HttpClient httpClient,
        ITokenStore tokenStore,
        string clientId,
        Uri tokenEndpoint,
        Uri supabaseUrl,
        string publishableKey,
        Func<DateTimeOffset>? clock = null)
    {
        this.httpClient = httpClient;
        this.tokenStore = tokenStore;
        this.clientId = clientId;
        this.tokenEndpoint = tokenEndpoint;
        this.publishableKey = publishableKey;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        profileEndpoint = new Uri(
            $"{supabaseUrl.AbsoluteUri.TrimEnd('/')}/rest/v1/app_profiles?select=id&limit=1");
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
            var response = await SendProfileRequestAsync(
                HttpMethod.Get,
                token,
                content: null,
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
                    $"Supabase profile request failed with HTTP {(int)response.StatusCode}.");
            }

            var profiles = await response.Content.ReadFromJsonAsync<
                IReadOnlyList<SupabaseProfileRow>>(
                cancellationToken: cancellationToken);
            if (profiles is { Count: > 0 })
            {
                return ToSession(profiles[0]);
            }

            response.Dispose();
            var created = await SendProfileRequestAsync(
                HttpMethod.Post,
                token,
                new StringContent("{}", Encoding.UTF8, "application/json"),
                cancellationToken);
            if (created.StatusCode == HttpStatusCode.Conflict)
            {
                created.Dispose();
                continue;
            }

            if (!created.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Supabase profile bootstrap failed with HTTP {(int)created.StatusCode}.");
            }

            var createdProfiles = await created.Content.ReadFromJsonAsync<
                IReadOnlyList<SupabaseProfileRow>>(
                cancellationToken: cancellationToken);
            var profile = createdProfiles is { Count: > 0 }
                ? createdProfiles[0]
                : throw new InvalidOperationException(
                    "Supabase profile bootstrap returned no profile.");
            return ToSession(profile);
        }

        throw new InvalidOperationException(
            "Supabase profile validation could not be completed.");
    }

    private async Task<HttpResponseMessage> SendProfileRequestAsync(
        HttpMethod method,
        TokenSet token,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token.IdToken))
        {
            throw new InvalidOperationException(
                "Auth0 ID token is required for Supabase profile access.");
        }

        var request = new HttpRequestMessage(method, profileEndpoint)
        {
            Content = content,
        };
        request.Headers.Add("apikey", publishableKey);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.IdToken);
        if (method == HttpMethod.Post)
        {
            request.Headers.Add("Prefer", "return=representation");
        }

        return await httpClient.SendAsync(request, cancellationToken);
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

        var refreshAt = token.IssuedAt + TimeSpan.FromSeconds(
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
                $"Auth0 session refresh failed with HTTP {(int)response.StatusCode}.");
        }

        var refreshed = await response.Content.ReadFromJsonAsync<TokenSet>(
            cancellationToken: cancellationToken);
        if (refreshed is null || string.IsNullOrWhiteSpace(refreshed.AccessToken))
        {
            throw new InvalidOperationException(
                "Auth0 refresh returned no access token.");
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

    private static SessionResponse ToSession(SupabaseProfileRow profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            throw new InvalidOperationException(
                "Supabase profile returned no stable profile id.");
        }

        return new SessionResponse(profile.Id, Authenticated: true);
    }

    private sealed record SupabaseProfileRow(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("auth0_subject")] string? Auth0Subject);
}
