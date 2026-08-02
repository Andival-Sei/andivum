using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Andivum.Api.Data;
using OpenIddict.Abstractions;
using Andivum.Api.Identity;
using Xunit;

namespace Andivum.Api.Tests;

public sealed class OpenIddictFlowTests
{
    [Fact]
    public async Task Discovery_document_is_available()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        using var response = await client.GetAsync(
            "/.well-known/openid-configuration");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("authorization_endpoint", body);
        Assert.Contains("token_endpoint", body);
    }

    [Fact]
    public async Task Session_endpoint_rejects_anonymous_requests()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        using var response = await client.GetAsync("/api/v1/session");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authorization_rejects_an_unknown_redirect_uri()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        using var response = await client.GetAsync(
            "/connect/authorize?client_id=andivum-windows" +
            "&response_type=code" +
            "&redirect_uri=https%3A%2F%2Fevil.example%2Fcallback" +
            "&scope=openid%20profile" +
            "&code_challenge=challenge" +
            "&code_challenge_method=S256");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Authorization_rejects_requests_without_pkce()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        using var response = await client.GetAsync(
            "/connect/authorize?client_id=andivum-windows" +
            "&response_type=code" +
            "&redirect_uri=andivum%3A%2F%2Fwindows%2Fauth%2Fcallback" +
            "&scope=openid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Authorization_rejects_plain_pkce_for_native_clients()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        using var response = await client.GetAsync(
            "/connect/authorize?client_id=andivum-windows" +
            "&response_type=code" +
            "&redirect_uri=andivum%3A%2F%2Fwindows%2Fauth%2Fcallback" +
            "&scope=openid" +
            "&code_challenge=challenge" +
            "&code_challenge_method=plain");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Public_native_client_rejects_a_client_secret()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        using var response = await client.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("client_id", "andivum-windows"),
                new KeyValuePair<string, string>("client_secret", "unexpected"),
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
            ]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "invalid_client",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Registered_native_client_reaches_the_passkey_surface()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        using var response = await client.GetAsync(
            "/connect/authorize?client_id=andivum-windows" +
            "&response_type=code" +
            "&redirect_uri=andivum%3A%2F%2Fwindows%2Fauth%2Fcallback" +
            "&scope=openid" +
            "&code_challenge=challenge" +
            "&code_challenge_method=S256");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        Assert.Contains("Continue with passkey", body);
        Assert.Contains("Create an account with email and password", body);
        Assert.Contains("Account settings", body);
        Assert.DoesNotContain("Create an account with passkey", body);
    }

    [Fact]
    public async Task Authorization_surface_offers_email_password_registration_and_sign_in()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        using var response = await client.GetAsync(CreateAuthorizationPath());

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        Assert.Contains("Sign in with email and password", body);
        Assert.Contains("Create an account with email and password", body);
        Assert.Contains("name=\"email\"", body);
        Assert.Contains("name=\"password\"", body);
        Assert.Contains("name=\"confirmPassword\"", body);
    }

    [Fact]
    public async Task Email_password_registration_completes_the_native_authorization_request()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(
            factory,
            allowAutoRedirect: false);
        var email = $"registration-{Guid.NewGuid():N}@example.test";

        using var pageResponse = await client.GetAsync(CreateAuthorizationPath());
        var page = await pageResponse.Content.ReadAsStringAsync();
        var antiForgeryToken = ExtractAntiForgeryToken(page);

        using var response = await client.PostAsync(
            "/connect/authorize",
            CreateAuthorizationForm(
            [
                new("action", "register"),
                new("email", email),
                new("password", "CorrectHorseBattery!27"),
                new("confirmPassword", "CorrectHorseBattery!27"),
                new("__RequestVerificationToken", antiForgeryToken),
            ]));

        var location = response.Headers.Location?.ToString();
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(location);
        Assert.Contains("code=", location);
        Assert.Contains("state=", location);
        Assert.DoesNotContain("CorrectHorseBattery!27", location);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.True(await userManager.CheckPasswordAsync(
            user!,
            "CorrectHorseBattery!27"));
    }

    [Fact]
    public async Task Email_password_login_completes_the_native_authorization_request()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(
            factory,
            allowAutoRedirect: false);
        var email = $"login-{Guid.NewGuid():N}@example.test";

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Email = email,
            UserName = email,
        };
        var createResult = await userManager.CreateAsync(
            user,
            "CorrectHorseBattery!27");
        Assert.True(createResult.Succeeded, string.Join(
            ", ",
            createResult.Errors.Select(error => error.Description)));

        using var pageResponse = await client.GetAsync(CreateAuthorizationPath());
        var page = await pageResponse.Content.ReadAsStringAsync();
        var antiForgeryToken = ExtractAntiForgeryToken(page);

        using var response = await client.PostAsync(
            "/connect/authorize",
            CreateAuthorizationForm(
            [
                new("action", "login"),
                new("email", email),
                new("password", "CorrectHorseBattery!27"),
                new("__RequestVerificationToken", antiForgeryToken),
            ]));

        var location = response.Headers.Location?.ToString();
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(location);
        Assert.Contains("code=", location);
        Assert.Contains("state=", location);
        Assert.DoesNotContain("CorrectHorseBattery!27", location);

        using var settingsResponse = await client.GetAsync("/Account/Settings");
        var settingsBody = await settingsResponse.Content.ReadAsStringAsync();
        Assert.True(settingsResponse.IsSuccessStatusCode, settingsBody);
        Assert.Contains("Account settings", settingsBody);
        Assert.Contains("Connect a passkey", settingsBody);
    }

    [Fact]
    public async Task Account_settings_rejects_anonymous_requests()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        using var response = await client.GetAsync("/Account/Settings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Email_password_authorization_without_csrf_is_rejected()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(
            factory,
            allowAutoRedirect: false);

        using var response = await client.PostAsync(
            "/connect/authorize",
            CreateAuthorizationForm(
            [
                new("action", "login"),
                new("email", "nobody@example.test"),
                new("password", "CorrectHorseBattery!27"),
            ]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_csrf", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Registered_native_client_accepts_offline_access_scope()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        using var response = await client.GetAsync(
            "/connect/authorize?client_id=andivum-windows" +
            "&response_type=code" +
            "&redirect_uri=andivum%3A%2F%2Fwindows%2Fauth%2Fcallback" +
            "&scope=openid%20profile%20offline_access" +
            "&code_challenge=challenge" +
            "&code_challenge_method=S256");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        Assert.Contains("Continue with passkey", body);
    }

    [Fact]
    public async Task Native_client_seeder_repairs_permissions_of_an_existing_client()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var registry = scope.ServiceProvider.GetRequiredService<NativeClientRegistry>();
        var existing = await manager.FindByClientIdAsync("andivum-windows");
        Assert.NotNull(existing);

        var legacyDescriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = "andivum-windows",
            ClientType = OpenIddictConstants.ClientTypes.Public,
            ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
            DisplayName = "andivum-windows",
        };
        legacyDescriptor.RedirectUris.Add(new Uri("andivum://windows/auth/callback"));
        legacyDescriptor.Permissions.UnionWith(
        [
            OpenIddictConstants.Permissions.Endpoints.Authorization,
            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
            OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
            OpenIddictConstants.Permissions.ResponseTypes.Code,
            OpenIddictConstants.Permissions.Scopes.Profile,
        ]);
        await manager.UpdateAsync(existing, legacyDescriptor);

        await NativeClientSeeder.SeedAsync(manager, registry);

        var repaired = await manager.FindByClientIdAsync("andivum-windows");
        Assert.NotNull(repaired);
        Assert.True(
            await manager.HasPermissionAsync(
                repaired,
                OpenIddictConstants.Permissions.Prefixes.Scope +
                OpenIddictConstants.Scopes.OfflineAccess));
    }

    [Fact]
    public async Task Passkey_registration_without_csrf_is_rejected()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        using var response = await client.PostAsJsonAsync(
            "/Account/PasskeyRegistrationStart",
            new { displayName = "Personal phone" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Passkey_mutations_without_csrf_are_rejected()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        using var response = await client.PostAsJsonAsync(
            "/Account/PasskeyCreationOptions",
            new { displayName = "Windows Hello" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Passkey_request_options_without_csrf_are_rejected()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        using var response = await client.PostAsync(
            "/Account/PasskeyRequestOptions",
            content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(
                    "ConnectionStrings:Postgres",
                    Environment.GetEnvironmentVariable(
                        "ConnectionStrings__Postgres") ??
                    throw new InvalidOperationException(
                        "ConnectionStrings__Postgres must be set by the dedicated test runner."));
                builder.UseSetting("Database:AutoMigrate", "true");
            });
    }

    private static HttpClient CreateHttpsClient(
        WebApplicationFactory<Program> factory,
        bool allowAutoRedirect = true)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = allowAutoRedirect,
        });
    }

    private static string CreateAuthorizationPath()
    {
        return "/connect/authorize?client_id=andivum-windows" +
            "&response_type=code" +
            "&redirect_uri=andivum%3A%2F%2Fwindows%2Fauth%2Fcallback" +
            "&scope=openid%20profile%20offline_access" +
            "&state=test-state" +
            "&code_challenge=test-code-challenge" +
            "&code_challenge_method=S256";
    }

    private static FormUrlEncodedContent CreateAuthorizationForm(
        IEnumerable<KeyValuePair<string, string>> values)
    {
        var formValues = new List<KeyValuePair<string, string>>
        {
            new("client_id", "andivum-windows"),
            new("response_type", "code"),
            new("redirect_uri", "andivum://windows/auth/callback"),
            new("scope", "openid profile offline_access"),
            new("state", "test-state"),
            new("code_challenge", "test-code-challenge"),
            new("code_challenge_method", "S256"),
        };
        formValues.AddRange(values);
        return new FormUrlEncodedContent(formValues);
    }

    private static string ExtractAntiForgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "name=\\\"__RequestVerificationToken\\\"[^>]*value=\\\"([^\\\"]+)\\\"",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, html);
        return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
