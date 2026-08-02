using System.Net;
using System.Text;
using Andivum_Windows.Auth;
using Xunit;

namespace Andivum.Windows.Tests;

public sealed class SupabaseProfileClientTests
{
    [Fact]
    public async Task It_bootstraps_a_profile_without_sending_an_owner_subject()
    {
        var now = new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);
        var store = new FakeTokenStore(
            new TokenSet(
                "access-token",
                "refresh-token",
                "Bearer",
                300,
                "openid profile offline_access",
                now,
                "id-token"));
        var handler = new SupabaseHandler();
        using var httpClient = new HttpClient(handler);
        var client = new SupabaseProfileClient(
            httpClient,
            store,
            "auth0-client",
            new Uri("https://dev-example.eu.auth0.com/oauth/token"),
            new Uri("https://example.supabase.co"),
            "publishable-key",
            () => now);

        var session = await client.GetCurrentSessionAsync();

        Assert.Equal("profile-123", session.UserId);
        Assert.True(session.Authenticated);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("publishable-key", handler.Requests[0].Headers.GetValues("apikey").Single());
        Assert.Equal("Bearer id-token", handler.Requests[0].Headers.Authorization?.ToString());
        var body = await handler.Requests[1].Content!.ReadAsStringAsync();
        Assert.DoesNotContain("auth0_subject", body, StringComparison.Ordinal);
    }

    private sealed class FakeTokenStore(TokenSet? initial) : ITokenStore
    {
        private TokenSet? tokenSet = initial;

        public TokenSet? Read() => tokenSet;

        public void Save(TokenSet value) => tokenSet = value;

        public void Clear() => tokenSet = null;
    }

    private sealed class SupabaseHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, "[]"));
            }

            return Task.FromResult(JsonResponse(
                HttpStatusCode.Created,
                "[{\"id\":\"profile-123\",\"auth0_subject\":\"auth0|alice\"}]"));
        }

        private static HttpResponseMessage JsonResponse(
            HttpStatusCode statusCode,
            string json)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        }
    }
}
