using System.Net;
using System.Text;
using Andivum_Windows.Auth;
using Xunit;

namespace Andivum.Windows.Tests;

public sealed class SupabaseProfileClientTests
{
    [Fact]
    public async Task It_bootstraps_a_profile_with_the_supabase_access_token()
    {
        var store = new FakeTokenStore(
            new TokenSet(
                "access-token",
                "refresh-token",
                "Bearer",
                300,
                null,
                DateTimeOffset.UtcNow));
        var handler = new SupabaseHandler();
        using var httpClient = new HttpClient(handler);
        var client = new SupabaseProfileClient(
            httpClient,
            store,
            new Uri("https://example.supabase.co"),
            "publishable-key");

        var session = await client.GetCurrentSessionAsync();

        Assert.Equal("user-123", session.UserId);
        Assert.True(session.Authenticated);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("publishable-key", handler.Requests[0].Headers.GetValues("apikey").Single());
        Assert.Equal("Bearer access-token", handler.Requests[0].Headers.Authorization?.ToString());
        Assert.Equal("{}", handler.RequestBodies[0]);
        Assert.DoesNotContain("auth0_subject", handler.RequestBodies[0], StringComparison.Ordinal);
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

        public List<string> RequestBodies { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (request.Content is not null)
            {
                RequestBodies.Add(request.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            }
            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, "[]"));
            }

            return Task.FromResult(JsonResponse(
                HttpStatusCode.Created,
                "[{\"id\":\"profile-123\",\"user_id\":\"user-123\"}]"));
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
