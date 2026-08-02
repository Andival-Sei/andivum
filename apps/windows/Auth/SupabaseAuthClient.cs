using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Andivum_Windows.Auth;

public sealed record AuthOperationResult(
    bool SessionCreated,
    bool EmailConfirmationRequired);

public sealed class SupabaseAuthClient
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromSeconds(30);

    private readonly HttpClient httpClient;
    private readonly ITokenStore tokenStore;
    private readonly Uri authBaseUri;
    private readonly string publishableKey;
    private readonly SupabaseProfileClient profileClient;
    private readonly Func<DateTimeOffset> clock;

    public SupabaseAuthClient(
        HttpClient httpClient,
        ITokenStore tokenStore,
        Uri supabaseUrl,
        string publishableKey,
        Func<DateTimeOffset>? clock = null)
    {
        this.httpClient = httpClient;
        this.tokenStore = tokenStore;
        authBaseUri = new Uri($"{supabaseUrl.AbsoluteUri.TrimEnd('/')}/auth/v1/");
        this.publishableKey = publishableKey;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        profileClient = new SupabaseProfileClient(
            httpClient,
            tokenStore,
            supabaseUrl,
            publishableKey);
    }

    public TokenSet? CurrentSession => tokenStore.Read();

    public async Task<AuthOperationResult> SignInAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default) =>
        await AuthenticateAsync(
            "token?grant_type=password",
            email,
            password,
            cancellationToken);

    public async Task<AuthOperationResult> SignUpAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default) =>
        await AuthenticateAsync("signup", email, password, cancellationToken);

    public async Task<SessionResponse> GetCurrentSessionAsync(
        CancellationToken cancellationToken = default)
    {
        var token = tokenStore.Read() ??
            throw new InvalidOperationException("No saved session is available.");
        var hasRefreshed = false;

        if (NeedsRefresh(token))
        {
            await RefreshAsync(token, cancellationToken);
            hasRefreshed = true;
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                return await profileClient.GetCurrentSessionAsync(cancellationToken);
            }
            catch (SessionUnauthorizedException) when
                (!hasRefreshed && !string.IsNullOrWhiteSpace(token.RefreshToken))
            {
                token = await RefreshAsync(token, cancellationToken);
                hasRefreshed = true;
            }
        }

        throw new InvalidOperationException(
            "Supabase session validation could not be completed.");
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        var token = tokenStore.Read();
        try
        {
            if (token is not null && !string.IsNullOrWhiteSpace(token.AccessToken))
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    new Uri(authBaseUri, "logout"));
                request.Headers.Add("apikey", publishableKey);
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    token.AccessToken);
                using var response = await httpClient.SendAsync(request, cancellationToken);
            }
        }
        finally
        {
            tokenStore.Clear();
        }
    }

    private async Task<AuthOperationResult> AuthenticateAsync(
        string path,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        ValidateCredentials(email, password);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(authBaseUri, path))
        {
            Content = JsonContent.Create(new { email, password }),
        };
        request.Headers.Add("apikey", publishableKey);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(FormatAuthError(response.StatusCode, body));
        }

        var payload = JsonSerializer.Deserialize<SupabaseAuthPayload>(body) ??
            throw new InvalidOperationException("Supabase Auth returned no response.");
        if (string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            return new AuthOperationResult(
                SessionCreated: false,
                EmailConfirmationRequired: path == "signup");
        }

        tokenStore.Save(payload.ToTokenSet(clock()));
        return new AuthOperationResult(
            SessionCreated: true,
            EmailConfirmationRequired: false);
    }

    private async Task<TokenSet> RefreshAsync(
        TokenSet current,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(current.RefreshToken))
        {
            throw new InvalidOperationException("No Supabase refresh token is available.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(authBaseUri, "token?grant_type=refresh_token"))
        {
            Content = JsonContent.Create(new { refresh_token = current.RefreshToken }),
        };
        request.Headers.Add("apikey", publishableKey);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(FormatAuthError(response.StatusCode, body));
        }

        var payload = JsonSerializer.Deserialize<SupabaseAuthPayload>(body) ??
            throw new InvalidOperationException("Supabase refresh returned no response.");
        if (string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new InvalidOperationException("Supabase refresh returned no access token.");
        }

        var refreshed = payload.ToTokenSet(clock()) with
        {
            RefreshToken = string.IsNullOrWhiteSpace(payload.RefreshToken)
                ? current.RefreshToken
                : payload.RefreshToken,
        };
        tokenStore.Save(refreshed);
        return refreshed;
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

    private static void ValidateCredentials(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password is required.", nameof(password));
        }
    }

    private static string FormatAuthError(HttpStatusCode statusCode, string body)
    {
        var detail = string.Empty;
        try
        {
            var error = JsonSerializer.Deserialize<SupabaseErrorPayload>(body);
            detail = error?.Message ?? error?.Msg ?? error?.ErrorDescription ?? string.Empty;
        }
        catch (JsonException)
        {
            // Keep the user-facing error generic when the service returns malformed JSON.
        }

        return string.IsNullOrWhiteSpace(detail)
            ? $"Supabase Auth request failed with HTTP {(int)statusCode}."
            : $"Supabase Auth: {detail}";
    }

    private sealed record SupabaseAuthPayload(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("token_type")] string? TokenType,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("scope")] string? Scope)
    {
        public TokenSet ToTokenSet(DateTimeOffset issuedAt) => new(
            AccessToken ?? throw new InvalidOperationException("Supabase response has no access token."),
            RefreshToken,
            string.IsNullOrWhiteSpace(TokenType) ? "Bearer" : TokenType,
            ExpiresIn > 0 ? ExpiresIn : 3600,
            Scope,
            issuedAt);
    }

    private sealed record SupabaseErrorPayload(
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("msg")] string? Msg,
        [property: JsonPropertyName("error_description")] string? ErrorDescription);
}
