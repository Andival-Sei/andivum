using System.Net;
using System.Text;
using Andivum_Windows.Auth;
using Xunit;

namespace Andivum.Windows.Tests;

public sealed class ProtectedSessionClientTests
{
    [Fact]
    public async Task Expired_access_token_is_refreshed_before_session_request()
    {
        var now = new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);
        var store = new FakeTokenStore(
            new TokenSet(
                "old-access",
                "old-refresh",
                "Bearer",
                300,
                "openid offline_access",
                now.AddMinutes(-10)));
        var handler = new SessionHandler();
        using var httpClient = new HttpClient(handler);
        var client = new ProtectedSessionClient(
            httpClient,
            store,
            "andivum-windows",
            new Uri("https://api.test/connect/token"),
            new Uri("https://api.test/api/v1/session"),
            () => now);

        var session = await client.GetCurrentSessionAsync();

        Assert.Equal("account-123", session.UserId);
        Assert.Equal("new-access", store.Read()!.AccessToken);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("old-refresh", handler.RefreshTokenUsed);
        var authorization = handler.Requests[1].Headers.Authorization;
        Assert.NotNull(authorization);
        Assert.Equal("Bearer", authorization!.Scheme);
        Assert.Equal("new-access", authorization.Parameter);
    }

    private sealed class FakeTokenStore(TokenSet? initial) : ITokenStore
    {
        private TokenSet? tokenSet = initial;

        public TokenSet? Read() => tokenSet;

        public void Save(TokenSet value) => tokenSet = value;

        public void Clear() => tokenSet = null;
    }

    private sealed class SessionHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public string? RefreshTokenUsed { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            if (request.RequestUri!.AbsolutePath == "/connect/token")
            {
                var form = await request.Content!.ReadAsStringAsync(cancellationToken);
                RefreshTokenUsed = Uri.UnescapeDataString(
                    form.Split("refresh_token=", StringSplitOptions.None)[1]
                        .Split('&', 2)[0]);
                return JsonResponse(
                    "{\"access_token\":\"new-access\",\"refresh_token\":\"new-refresh\",\"token_type\":\"Bearer\",\"expires_in\":300,\"scope\":\"openid offline_access\"}");
            }

            return JsonResponse("{\"userId\":\"account-123\",\"authenticated\":true}");
        }

        private static HttpResponseMessage JsonResponse(string json)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        }
    }
}
