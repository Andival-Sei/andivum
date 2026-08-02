using Andivum.Api.Data;
using Andivum.Api.Identity;
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

builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>()
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
        options.RegisterScopes(OpenIddictConstants.Scopes.Profile);
        options.SetAuthorizationEndpointUris("connect/authorize")
            .SetTokenEndpointUris("connect/token")
            .AllowAuthorizationCodeFlow()
            .AllowRefreshTokenFlow()
            .RequireProofKeyForCodeExchange()
            .SetAccessTokenLifetime(TimeSpan.FromMinutes(5))
            .SetRefreshTokenLifetime(TimeSpan.FromDays(30))
            .AddEphemeralEncryptionKey()
            .AddEphemeralSigningKey()
            .UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.AddControllers();

var app = builder.Build();

_ = app.Services.GetRequiredService<IOptions<IdentityPasskeyOptions>>().Value;

if (app.Environment.IsDevelopment() &&
    !string.Equals(
        app.Configuration["Database:AutoMigrate"],
        "false",
        StringComparison.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await database.Database.MigrateAsync();
    await NativeClientSeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>(),
        scope.ServiceProvider.GetRequiredService<NativeClientRegistry>());
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapPasskeyEndpoints();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet(
        "/api/v1/session",
        (HttpContext context) => Results.Ok(new
        {
            userId = context.User.FindFirst(
                OpenIddictConstants.Claims.Subject)?.Value,
            authenticated = true,
        }))
    .RequireAuthorization(new AuthorizeAttribute
    {
        AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
    });

app.Run();

public partial class Program;
