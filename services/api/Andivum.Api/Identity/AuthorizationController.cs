using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Andivum.Api.Data;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace Andivum.Api.Identity;

[ApiController]
public sealed class AuthorizationController : ControllerBase
{
    [HttpGet("~/connect/authorize")]
    public async Task<IActionResult> Authorize(
        IAntiforgery antiforgery,
        UserManager<ApplicationUser> userManager)
    {
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request is null)
        {
            return BadRequest(new { code = "invalid_authorization_request" });
        }

        var user = await userManager.GetUserAsync(User);
        return user is null
            ? LoginPageContent(antiforgery, HttpContext.Request.Query)
            : IssueAuthorization(request, user);
    }

    [HttpPost("~/connect/authorize")]
    public async Task<IActionResult> AuthorizePost(
        IAntiforgery antiforgery,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        var form = await HttpContext.Request.ReadFormAsync();
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request is null)
        {
            return BadRequest(new { code = "invalid_authorization_request" });
        }

        if (!await IsAntiForgeryValidAsync(HttpContext, antiforgery))
        {
            return BadRequest(new { code = "invalid_csrf" });
        }

        var email = form["email"].ToString().Trim();
        var password = form["password"].ToString();
        var action = form["action"].ToString();

        if (string.Equals(action, "register", StringComparison.Ordinal))
        {
            if (!string.Equals(
                    password,
                    form["confirmPassword"].ToString(),
                    StringComparison.Ordinal))
            {
                return LoginPageContent(
                    antiforgery,
                    form,
                    email,
                    "Registration failed. Check the email and password fields.");
            }

            var user = new ApplicationUser
            {
                Email = email,
                UserName = email,
            };
            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                return LoginPageContent(
                    antiforgery,
                    form,
                    email,
                    "Registration failed. Use a valid email and a strong password, or try another email.");
            }

            await signInManager.SignInAsync(user, isPersistent: false);
            return IssueAuthorization(request, user);
        }

        if (string.Equals(action, "login", StringComparison.Ordinal))
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                return LoginPageContent(
                    antiforgery,
                    form,
                    email,
                    "Sign-in failed. Check your email and password and try again.");
            }

            var passwordResult = await signInManager.CheckPasswordSignInAsync(
                user,
                password,
                lockoutOnFailure: true);
            if (!passwordResult.Succeeded)
            {
                return LoginPageContent(
                    antiforgery,
                    form,
                    email,
                    "Sign-in failed. Check your email and password and try again.");
            }

            await signInManager.SignInAsync(user, isPersistent: false);
            return IssueAuthorization(request, user);
        }

        return LoginPageContent(
            antiforgery,
            form,
            email,
            "Choose sign in or create an account.");
    }

    [HttpGet("~/Account/Settings")]
    public async Task<IActionResult> AccountSettings(
        IAntiforgery antiforgery,
        UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        var passkeys = await userManager.GetPasskeysAsync(user);
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        var token = WebUtility.HtmlEncode(tokens.RequestToken);
        var tokenJson = JsonSerializer.Serialize(tokens.RequestToken);

        return Content(
            AccountSettingsPage
                .Replace("__ANTIFORGERY_INPUT__", token, StringComparison.Ordinal)
                .Replace("__ANTIFORGERY_TOKEN__", tokenJson, StringComparison.Ordinal)
                .Replace("__PASSKEY_COUNT__", passkeys.Count.ToString(), StringComparison.Ordinal),
            "text/html; charset=utf-8");
    }

    [HttpPost("~/connect/token")]
    public async Task<IActionResult> ExchangeToken()
    {
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request is null ||
            (!request.IsAuthorizationCodeGrantType() &&
             !request.IsRefreshTokenGrantType()))
        {
            return BadRequest(new { code = "unsupported_grant_type" });
        }

        var result = await HttpContext.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Principal is null)
        {
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return SignIn(
            result.Principal,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private IActionResult IssueAuthorization(
        OpenIddictRequest request,
        ApplicationUser user)
    {
        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        identity.AddClaim(new Claim(
            OpenIddictConstants.Claims.Subject,
            user.Id.ToString()));

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        principal.SetResources("andivum-api");

        return SignIn(
            principal,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private ContentResult LoginPageContent(
        IAntiforgery antiforgery,
        IEnumerable<KeyValuePair<string, StringValues>> authorizationParameters,
        string? email = null,
        string? error = null)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        var hiddenFields = RenderHiddenAuthorizationFields(authorizationParameters);
        var antiForgeryToken = WebUtility.HtmlEncode(tokens.RequestToken);
        var tokenJson = JsonSerializer.Serialize(tokens.RequestToken);
        var emailValue = WebUtility.HtmlEncode(email ?? string.Empty);
        var errorMarkup = string.IsNullOrWhiteSpace(error)
            ? string.Empty
            : $"<p class=\"error\" role=\"alert\">{WebUtility.HtmlEncode(error)}</p>";

        return Content(
            LoginPage
                .Replace("__AUTHORIZATION_FIELDS__", hiddenFields, StringComparison.Ordinal)
                .Replace("__ANTIFORGERY_INPUT__", antiForgeryToken, StringComparison.Ordinal)
                .Replace("__ANTIFORGERY_TOKEN__", tokenJson, StringComparison.Ordinal)
                .Replace("__EMAIL__", emailValue, StringComparison.Ordinal)
                .Replace("__ERROR__", errorMarkup, StringComparison.Ordinal),
            "text/html; charset=utf-8");
    }

    private static string RenderHiddenAuthorizationFields(
        IEnumerable<KeyValuePair<string, StringValues>> parameters)
    {
        var builder = new StringBuilder();
        foreach (var parameter in parameters)
        {
            if (NonAuthorizationFormFields.Contains(parameter.Key))
            {
                continue;
            }

            foreach (var value in parameter.Value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                builder
                    .Append("<input type=\"hidden\" name=\"")
                    .Append(WebUtility.HtmlEncode(parameter.Key))
                    .Append("\" value=\"")
                    .Append(WebUtility.HtmlEncode(value))
                    .Append("\" />");
            }
        }

        return builder.ToString();
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

    private static readonly HashSet<string> NonAuthorizationFormFields =
        new(StringComparer.Ordinal)
        {
            "action",
            "email",
            "password",
            "confirmPassword",
            "__RequestVerificationToken",
        };

    private const string LoginPage = """
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>Andivum sign in</title>
          <style>
            body { font-family: system-ui, sans-serif; max-width: 680px; margin: 3rem auto; padding: 0 1rem; }
            form { border: 1px solid #d5d9e0; border-radius: 12px; padding: 1.25rem; margin: 1rem 0; }
            label { display: block; margin-top: .75rem; }
            input { box-sizing: border-box; display: block; width: 100%; padding: .65rem; margin-top: .25rem; }
            button { margin-top: 1rem; padding: .65rem 1rem; cursor: pointer; }
            .error { color: #a40000; }
            .hint { color: #5d6570; }
          </style>
        </head>
        <body>
          <main>
            <h1>Sign in to Andivum</h1>
            <p class="hint">Your password is entered only on this secure server page. The app never receives it.</p>
            __ERROR__
            <form method="post" action="/connect/authorize">
              __AUTHORIZATION_FIELDS__
              <input type="hidden" name="__RequestVerificationToken" value="__ANTIFORGERY_INPUT__" />
              <input type="hidden" name="action" value="login" />
              <h2>Sign in with email and password</h2>
              <label for="login-email">Email</label>
              <input id="login-email" name="email" type="email" value="__EMAIL__" autocomplete="email" required />
              <label for="login-password">Password</label>
              <input id="login-password" name="password" type="password" autocomplete="current-password" required />
              <button type="submit">Sign in</button>
            </form>
            <form method="post" action="/connect/authorize">
              __AUTHORIZATION_FIELDS__
              <input type="hidden" name="__RequestVerificationToken" value="__ANTIFORGERY_INPUT__" />
              <input type="hidden" name="action" value="register" />
              <h2>Create an account with email and password</h2>
              <label for="registration-email">Email</label>
              <input id="registration-email" name="email" type="email" value="__EMAIL__" autocomplete="email" required />
              <label for="registration-password">Password</label>
              <input id="registration-password" name="password" type="password" autocomplete="new-password" minlength="12" required />
              <label for="registration-confirm-password">Repeat password</label>
              <input id="registration-confirm-password" name="confirmPassword" type="password" autocomplete="new-password" minlength="12" required />
              <p class="hint">Use at least 12 characters with upper/lowercase letters, a number and a symbol.</p>
              <button type="submit">Create account</button>
            </form>
            <h2>Already have a passkey?</h2>
            <button id="sign-in" type="button">Continue with passkey</button>
            <p class="hint">After signing in, you can connect a passkey in Account settings.</p>
            <p id="status" role="status"></p>
          </main>
          <script>
            const status = document.getElementById('status');
            const antiForgeryToken = __ANTIFORGERY_TOKEN__;
            const toBase64Url = (value) => {
              if (!value) return null;
              const bytes = value instanceof ArrayBuffer ? new Uint8Array(value) : value;
              let binary = '';
              for (const byte of bytes) binary += String.fromCharCode(byte);
              return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '');
            };
            const serializeCredential = (credential) => ({
              authenticatorAttachment: credential.authenticatorAttachment,
              clientExtensionResults: credential.getClientExtensionResults(),
              id: credential.id,
              rawId: toBase64Url(credential.rawId),
              response: {
                authenticatorData: toBase64Url(credential.response.authenticatorData),
                clientDataJSON: toBase64Url(credential.response.clientDataJSON),
                signature: toBase64Url(credential.response.signature),
                userHandle: toBase64Url(credential.response.userHandle),
              },
              type: credential.type,
            });
            document.getElementById('sign-in').addEventListener('click', async () => {
              try {
                status.textContent = 'Waiting for the authenticator…';
                const optionsResponse = await fetch('/Account/PasskeyRequestOptions', {
                  method: 'POST',
                  headers: { 'RequestVerificationToken': antiForgeryToken },
                });
                const options = PublicKeyCredential.parseRequestOptionsFromJSON(await optionsResponse.json());
                const credential = await navigator.credentials.get({ publicKey: options });
                const signInResponse = await fetch('/Account/PasskeySignIn', {
                  method: 'POST',
                  headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': antiForgeryToken,
                  },
                  body: JSON.stringify({ credentialJson: JSON.stringify(serializeCredential(credential)) }),
                });
                if (!signInResponse.ok) throw new Error('The passkey was not accepted.');
                location.reload();
              } catch (error) {
                status.textContent = error.message;
              }
            });
          </script>
        </body>
        </html>
        """;

    private const string AccountSettingsPage = """
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>Andivum account settings</title>
          <style>
            body { font-family: system-ui, sans-serif; max-width: 680px; margin: 3rem auto; padding: 0 1rem; }
            form { border: 1px solid #d5d9e0; border-radius: 12px; padding: 1.25rem; margin: 1rem 0; }
            label { display: block; margin-top: .75rem; }
            input { box-sizing: border-box; display: block; width: 100%; padding: .65rem; margin-top: .25rem; }
            button { margin-top: 1rem; padding: .65rem 1rem; cursor: pointer; }
          </style>
        </head>
        <body>
          <main>
            <h1>Account settings</h1>
            <p>Connected passkeys: <strong>__PASSKEY_COUNT__</strong></p>
            <form id="passkey-form">
              <input type="hidden" name="__RequestVerificationToken" value="__ANTIFORGERY_INPUT__" />
              <h2>Connect a passkey</h2>
              <label for="registration-name">Passkey name</label>
              <input id="registration-name" value="My passkey" maxlength="64" required />
              <button id="register" type="submit">Connect passkey</button>
            </form>
            <p id="status" role="status"></p>
          </main>
          <script>
            const status = document.getElementById('status');
            const antiForgeryToken = __ANTIFORGERY_TOKEN__;
            const toBase64Url = (value) => {
              if (!value) return null;
              const bytes = value instanceof ArrayBuffer ? new Uint8Array(value) : value;
              let binary = '';
              for (const byte of bytes) binary += String.fromCharCode(byte);
              return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '');
            };
            const credentialResponse = (credential) => {
              const response = credential.response;
              return {
                clientDataJSON: toBase64Url(response.clientDataJSON),
                authenticatorData: toBase64Url(response.authenticatorData),
                signature: toBase64Url(response.signature),
                userHandle: toBase64Url(response.userHandle),
                attestationObject: toBase64Url(response.attestationObject),
                transports: response.getTransports ? response.getTransports() : [],
              };
            };
            document.getElementById('passkey-form').addEventListener('submit', async (event) => {
              event.preventDefault();
              try {
                status.textContent = 'Preparing passkey registration…';
                const displayName = document.getElementById('registration-name').value;
                const optionsResponse = await fetch('/Account/PasskeyRegistrationStart', {
                  method: 'POST',
                  headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': antiForgeryToken,
                  },
                  body: JSON.stringify({ displayName }),
                });
                if (!optionsResponse.ok) throw new Error('Registration could not be started.');
                const options = PublicKeyCredential.parseCreationOptionsFromJSON(await optionsResponse.json());
                const credential = await navigator.credentials.create({ publicKey: options });
                const attestationResponse = await fetch('/Account/PasskeyAttestation', {
                  method: 'POST',
                  headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': antiForgeryToken,
                  },
                  body: JSON.stringify({
                    credentialJson: JSON.stringify({
                      authenticatorAttachment: credential.authenticatorAttachment,
                      clientExtensionResults: credential.getClientExtensionResults(),
                      id: credential.id,
                      rawId: toBase64Url(credential.rawId),
                      response: credentialResponse(credential),
                      type: credential.type,
                    }),
                    displayName,
                  }),
                });
                if (!attestationResponse.ok) throw new Error('The passkey was not registered.');
                status.textContent = 'Passkey connected.';
                location.reload();
              } catch (error) {
                status.textContent = error.message;
              }
            });
          </script>
        </body>
        </html>
        """;
}
