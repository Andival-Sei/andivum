using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace Andivum_Windows.Auth;

public sealed class SupabaseProfileClient
{
    private readonly HttpClient httpClient;
    private readonly ITokenStore tokenStore;
    private readonly Uri profileEndpoint;
    private readonly string publishableKey;

    public SupabaseProfileClient(
        HttpClient httpClient,
        ITokenStore tokenStore,
        Uri supabaseUrl,
        string publishableKey)
    {
        this.httpClient = httpClient;
        this.tokenStore = tokenStore;
        this.publishableKey = publishableKey;
        profileEndpoint = new Uri(
            $"{supabaseUrl.AbsoluteUri.TrimEnd('/')}/rest/v1/app_profiles?select=id,user_id&limit=1");
    }

    public async Task<SessionResponse> GetCurrentSessionAsync(
        CancellationToken cancellationToken = default)
    {
        var token = tokenStore.Read() ??
            throw new InvalidOperationException("No saved session is available.");
        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new InvalidOperationException("Supabase access token is required.");
        }

        using var response = await SendProfileRequestAsync(
            HttpMethod.Get,
            token.AccessToken,
            content: null,
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new SessionUnauthorizedException();
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Supabase profile request failed with HTTP {(int)response.StatusCode}.");
        }

        var profiles = await response.Content.ReadFromJsonAsync<
            IReadOnlyList<SupabaseProfileRow>>(cancellationToken: cancellationToken);
        if (profiles is { Count: > 0 })
        {
            return ToSession(profiles[0]);
        }

        using var created = await SendProfileRequestAsync(
            HttpMethod.Post,
            token.AccessToken,
            new StringContent("{}", Encoding.UTF8, "application/json"),
            cancellationToken);
        if (created.StatusCode == HttpStatusCode.Conflict)
        {
            return await GetCurrentSessionAsync(cancellationToken);
        }

        if (!created.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Supabase profile bootstrap failed with HTTP {(int)created.StatusCode}.");
        }

        var createdProfiles = await created.Content.ReadFromJsonAsync<
            IReadOnlyList<SupabaseProfileRow>>(cancellationToken: cancellationToken);
        var profile = createdProfiles is { Count: > 0 }
            ? createdProfiles[0]
            : throw new InvalidOperationException(
                "Supabase profile bootstrap returned no profile.");
        return ToSession(profile);
    }

    private async Task<HttpResponseMessage> SendProfileRequestAsync(
        HttpMethod method,
        string accessToken,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, profileEndpoint)
        {
            Content = content,
        };
        request.Headers.Add("apikey", publishableKey);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            accessToken);
        if (method == HttpMethod.Post)
        {
            request.Headers.Add("Prefer", "return=representation");
        }

        // HttpClient.SendAsync owns the response; the request can be disposed
        // immediately after the send completes.
        return await httpClient.SendAsync(request, cancellationToken);
    }

    private static SessionResponse ToSession(SupabaseProfileRow profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Id) ||
            string.IsNullOrWhiteSpace(profile.UserId))
        {
            throw new InvalidOperationException(
                "Supabase profile returned no stable user id.");
        }

        return new SessionResponse(profile.UserId, Authenticated: true);
    }

    private sealed record SupabaseProfileRow(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("user_id")] string? UserId);
}
