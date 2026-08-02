using Andivum.Api.Data;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

namespace Andivum.Api.Identity;

public static class PasskeyEndpoints
{
    public static IEndpointRouteBuilder MapPasskeyEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/Account/PasskeyRequestOptions",
            async (
                HttpContext context,
                IAntiforgery antiforgery,
                UserManager<ApplicationUser> userManager,
                SignInManager<ApplicationUser> signInManager) =>
            {
                if (!await IsAntiForgeryValidAsync(context, antiforgery))
                {
                    return Results.BadRequest(new { code = "invalid_csrf" });
                }

                var user = (ApplicationUser?)null;
                var optionsJson =
                    await signInManager.MakePasskeyRequestOptionsAsync(user);

                return TypedResults.Content(
                    optionsJson,
                    contentType: "application/json");
            });

        endpoints.MapPost(
            "/Account/PasskeyCreationOptions",
            async (
                HttpContext context,
                IAntiforgery antiforgery,
                UserManager<ApplicationUser> userManager,
                SignInManager<ApplicationUser> signInManager,
                PasskeyCreationRequest request) =>
            {
                if (!await IsAntiForgeryValidAsync(context, antiforgery))
                {
                    return Results.BadRequest(new { code = "invalid_csrf" });
                }

                var user = await userManager.GetUserAsync(context.User);
                if (user is null)
                {
                    return Results.Unauthorized();
                }

                var passkeys = await userManager.GetPasskeysAsync(user);
                if (!AuthPolicy.CanAddPasskey(passkeys.Count) ||
                    !AuthPolicy.IsPasskeyDisplayNameAllowed(request.DisplayName))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["displayName"] = ["The passkey name is invalid."],
                    });
                }

                var userId = await userManager.GetUserIdAsync(user);
                var userName = await userManager.GetUserNameAsync(user) ?? userId;
                var optionsJson =
                    await signInManager.MakePasskeyCreationOptionsAsync(new()
                    {
                        Id = userId,
                        Name = userName,
                        DisplayName = request.DisplayName,
                    });

                return TypedResults.Content(
                    optionsJson,
                    contentType: "application/json");
            })
            .RequireAuthorization();

        endpoints.MapPost(
            "/Account/PasskeyRegistrationStart",
            async (
                HttpContext context,
                IAntiforgery antiforgery,
                UserManager<ApplicationUser> userManager,
                SignInManager<ApplicationUser> signInManager,
                PasskeyRegistrationRequest request) =>
            {
                if (!await IsAntiForgeryValidAsync(context, antiforgery))
                {
                    return Results.BadRequest(new { code = "invalid_csrf" });
                }

                if (!AuthPolicy.IsPasskeyDisplayNameAllowed(request.DisplayName))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["displayName"] = ["The passkey name is invalid."],
                    });
                }

                var user = await userManager.GetUserAsync(context.User);
                if (user is not null)
                {
                    var existingPasskeys = await userManager.GetPasskeysAsync(user);
                    if (AuthPolicy.CanAuthorizeWithPasskey(existingPasskeys.Count))
                    {
                        return Results.BadRequest(new { code = "already_registered" });
                    }
                }
                else
                {
                    user = new ApplicationUser
                    {
                        UserName = $"passkey-{Guid.NewGuid():N}",
                    };
                    var createResult = await userManager.CreateAsync(user);
                    if (!createResult.Succeeded)
                    {
                        return Results.BadRequest(new { code = "registration_failed" });
                    }
                }

                await signInManager.SignInAsync(user, isPersistent: false);

                var optionsJson = await signInManager.MakePasskeyCreationOptionsAsync(
                    new()
                    {
                        Id = user.Id.ToString(),
                        Name = user.UserName!,
                        DisplayName = request.DisplayName,
                    });

                return TypedResults.Content(
                    optionsJson,
                    contentType: "application/json");
            });

        endpoints.MapPost(
            "/Account/PasskeyAttestation",
            async (
                HttpContext context,
                IAntiforgery antiforgery,
                UserManager<ApplicationUser> userManager,
                SignInManager<ApplicationUser> signInManager,
                PasskeyAttestationRequest request) =>
            {
                if (!await IsAntiForgeryValidAsync(context, antiforgery))
                {
                    return Results.BadRequest(new { code = "invalid_csrf" });
                }

                var user = await userManager.GetUserAsync(context.User);
                if (user is null)
                {
                    return Results.Unauthorized();
                }

                var passkeys = await userManager.GetPasskeysAsync(user);
                if (!AuthPolicy.CanAddPasskey(passkeys.Count) ||
                    !AuthPolicy.IsPasskeyDisplayNameAllowed(request.DisplayName))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["displayName"] = ["The passkey name is invalid."],
                    });
                }

                var result = await signInManager.PerformPasskeyAttestationAsync(
                    request.CredentialJson);
                if (!result.Succeeded)
                {
                    return Results.BadRequest(new { code = "invalid_passkey" });
                }

                result.Passkey!.Name = request.DisplayName;
                var storeResult = await userManager.AddOrUpdatePasskeyAsync(
                    user,
                    result.Passkey);
                if (!storeResult.Succeeded)
                {
                    return Results.BadRequest(new { code = "passkey_not_stored" });
                }

                return Results.Ok(new { registered = true });
            })
            .RequireAuthorization();

        endpoints.MapPost(
            "/Account/PasskeySignIn",
            async (
                HttpContext context,
                IAntiforgery antiforgery,
                SignInManager<ApplicationUser> signInManager,
                PasskeySignInRequest request) =>
            {
                if (!await IsAntiForgeryValidAsync(context, antiforgery))
                {
                    return Results.BadRequest(new { code = "invalid_csrf" });
                }

                var result = await signInManager.PasskeySignInAsync(
                    request.CredentialJson);

                return result.Succeeded
                    ? Results.Ok(new { authenticated = true })
                    : Results.Unauthorized();
            });

        return endpoints;
    }

    private static async Task<bool> IsAntiForgeryValidAsync(
        HttpContext context,
        IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }
}

public sealed record PasskeyCreationRequest(string DisplayName);

public sealed record PasskeyRegistrationRequest(string DisplayName);

public sealed record PasskeyAttestationRequest(
    string CredentialJson,
    string DisplayName);

public sealed record PasskeySignInRequest(string CredentialJson);
