using Andivum.Api.Data;
using Andivum.Api.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Postgres must be configured.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredUniqueChars = 5;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.SignIn.RequireConfirmedAccount = false;
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IConfigureOptions<IdentityPasskeyOptions>, IdentityOptionsSetup>();
builder.Services.AddOptions<IdentityPasskeyOptions>().ValidateOnStart();
builder.Services.AddSingleton(NativeClientRegistry.CreateDevelopment());

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<ApplicationDbContext>();
    })
    .AddServer(options =>
    {
        options.RegisterScopes(
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.OfflineAccess);
        options.SetAuthorizationEndpointUris("connect/authorize")
            .SetTokenEndpointUris("connect/token")
            .AllowAuthorizationCodeFlow()
            .AllowRefreshTokenFlow()
            .RequireProofKeyForCodeExchange()
            .SetAccessTokenLifetime(TimeSpan.FromMinutes(5))
            .SetRefreshTokenLifetime(TimeSpan.FromDays(30));

        if (builder.Environment.IsDevelopment())
        {
            options.AddEphemeralEncryptionKey()
                .AddEphemeralSigningKey();
        }
        else
        {
            options.AddEncryptionCertificate(
                    OpenIddictCredentialLoader.Load(
                        builder.Configuration,
                        "EncryptionCertificatePath"))
                .AddSigningCertificate(
                    OpenIddictCredentialLoader.Load(
                        builder.Configuration,
                        "SigningCertificatePath"));
        }

        options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.AddControllers();
builder.Services.AddAntiforgery();

var app = builder.Build();

_ = app.Services.GetRequiredService<IOptions<IdentityPasskeyOptions>>().Value;

var autoMigrate = app.Environment.IsDevelopment() &&
    string.Equals(
        app.Configuration["Database:AutoMigrate"],
        "true",
        StringComparison.OrdinalIgnoreCase);

if (autoMigrate &&
    !DevelopmentDatabasePolicy.IsLocalDevelopmentConnection(connectionString))
{
    throw new InvalidOperationException(
        "Development auto-migration requires a local andivum_* PostgreSQL database.");
}

if (autoMigrate)
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await database.Database.MigrateAsync();
    await NativeClientSeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>(),
        scope.ServiceProvider.GetRequiredService<NativeClientRegistry>());
}

var nativeClients = app.Services.GetRequiredService<NativeClientRegistry>();
app.Use(async (context, next) =>
{
    var clientId = context.Request.Query["client_id"].ToString();
    IFormCollection? form = null;

    if (context.Request.HasFormContentType)
    {
        form = await context.Request.ReadFormAsync();
        clientId = string.IsNullOrEmpty(clientId)
            ? form["client_id"].ToString()
            : clientId;
    }

    var codeChallenge = context.Request.Query["code_challenge"].ToString();
    var codeChallengeMethod = context.Request.Query["code_challenge_method"].ToString();
    if (form is not null)
    {
        codeChallenge = string.IsNullOrEmpty(codeChallenge)
            ? form["code_challenge"].ToString()
            : codeChallenge;
        codeChallengeMethod = string.IsNullOrEmpty(codeChallengeMethod)
            ? form["code_challenge_method"].ToString()
            : codeChallengeMethod;
    }

    if (context.Request.Path.Equals("/connect/authorize") &&
        nativeClients.IsRegistered(clientId) &&
        !AuthPolicy.IsS256PkceRequest(codeChallenge, codeChallengeMethod))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "invalid_request",
            error_description = "S256 PKCE is required for native clients.",
        });
        return;
    }

    if (context.Request.Path.Equals("/connect/token") &&
        nativeClients.IsRegistered(clientId) &&
        context.Request.HasFormContentType &&
        form is not null &&
        !string.IsNullOrEmpty(form["client_secret"].ToString()))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "invalid_client",
            error_description = "Native clients must not send a client secret.",
        });
        return;
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapPasskeyEndpoints();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet(
        "/api/v1/session",
        (HttpContext context) => Results.Ok(
            SessionEndpoint.CreateResponse(context.User)))
    .RequireAuthorization(new AuthorizeAttribute
    {
        AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
    });

app.Run();

public partial class Program;
