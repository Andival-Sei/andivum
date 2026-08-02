using System.Net;
using System.Text;
using Andivum_Windows.Auth;
using Xunit;

namespace Andivum.Windows.Tests;

public sealed class SupabaseAuthClientTests
{
    [Fact]
    public async Task Sign_in_posts_credentials_to_supabase_and_saves_the_session()
    {
        var store = new FakeTokenStore();
        var handler = new SupabaseAuthHandler(_ => JsonResponse(
            HttpStatusCode.OK,
            "{\"access_token\":\"access\",\"refresh_token\":\"refresh\",\"token_type\":\"bearer\",\"expires_in\":3600}"));
        using var httpClient = new HttpClient(handler);
        var client = new SupabaseAuthClient(
            httpClient,
            store,
            new Uri("https://example.supabase.co"),
            "publishable-key");

        var result = await client.SignInAsync("alice@example.com", "correct horse battery staple");

        Assert.True(result.SessionCreated);
        Assert.False(result.EmailConfirmationRequired);
        Assert.Equal("access", store.Read()!.AccessToken);
        var request = handler.Requests.Single();
        Assert.Equal("/auth/v1/token", request.RequestUri!.AbsolutePath);
        Assert.Equal("?grant_type=password", request.RequestUri.Query);
        Assert.Equal("publishable-key", request.Headers.GetValues("apikey").Single());
        var body = handler.RequestBodies.Single();
        Assert.Contains("alice@example.com", body, StringComparison.Ordinal);
        Assert.Contains("correct horse battery staple", body, StringComparison.Ordinal);
        Assert.DoesNotContain("auth0", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sign_up_without_a_session_reports_email_confirmation()
    {
        var store = new FakeTokenStore();
        var handler = new SupabaseAuthHandler(_ => JsonResponse(
            HttpStatusCode.OK,
            "{\"access_token\":\"\",\"refresh_token\":\"\",\"user\":{\"id\":\"user-123\"}}"));
        using var httpClient = new HttpClient(handler);
        var client = new SupabaseAuthClient(
            httpClient,
            store,
            new Uri("https://example.supabase.co"),
            "publishable-key");

        var result = await client.SignUpAsync("alice@example.com", "correct horse battery staple");

        Assert.False(result.SessionCreated);
        Assert.True(result.EmailConfirmationRequired);
        Assert.Null(store.Read());
        Assert.Equal("/auth/v1/signup", handler.Requests.Single().RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Expired_session_is_refreshed_before_profile_validation()
    {
        var now = new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);
        var store = new FakeTokenStore(new TokenSet(
            "old-access",
            "old-refresh",
            "bearer",
            300,
            null,
            now.AddMinutes(-10)));
        var handler = new SupabaseAuthHandler(request =>
            request.RequestUri!.AbsolutePath switch
            {
                "/auth/v1/token" => JsonResponse(
                    HttpStatusCode.OK,
                    "{\"access_token\":\"new-access\",\"refresh_token\":\"new-refresh\",\"token_type\":\"bearer\",\"expires_in\":3600}"),
                "/rest/v1/app_profiles" when request.Method == HttpMethod.Get => JsonResponse(
                    HttpStatusCode.OK,
                    "[{\"id\":\"profile-123\",\"user_id\":\"user-123\"}]"),
                _ => throw new InvalidOperationException("Unexpected request."),
            });
        using var httpClient = new HttpClient(handler);
        var client = new SupabaseAuthClient(
            httpClient,
            store,
            new Uri("https://example.supabase.co"),
            "publishable-key",
            () => now);

        var session = await client.GetCurrentSessionAsync();

        Assert.Equal("user-123", session.UserId);
        Assert.Equal("new-access", store.Read()!.AccessToken);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("Bearer new-access", handler.Requests[1].Headers.Authorization?.ToString());
    }

    [Fact]
    public async Task Sign_out_clears_the_local_session_even_when_logout_succeeds()
    {
        var store = new FakeTokenStore(new TokenSet(
            "access",
            "refresh",
            "bearer",
            3600,
            null,
            DateTimeOffset.UtcNow));
        var handler = new SupabaseAuthHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        using var httpClient = new HttpClient(handler);
        var client = new SupabaseAuthClient(
            httpClient,
            store,
            new Uri("https://example.supabase.co"),
            "publishable-key");

        await client.SignOutAsync();

        Assert.Null(store.Read());
        var request = handler.Requests.Single();
        Assert.Equal("/auth/v1/logout", request.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer access", request.Headers.Authorization?.ToString());
    }

    private sealed class FakeTokenStore(TokenSet? initial = null) : ITokenStore
    {
        private TokenSet? tokenSet = initial;

        public TokenSet? Read() => tokenSet;

        public void Save(TokenSet value) => tokenSet = value;

        public void Clear() => tokenSet = null;
    }

    private sealed class SupabaseAuthHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
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
            return Task.FromResult(responseFactory(request));
        }
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
