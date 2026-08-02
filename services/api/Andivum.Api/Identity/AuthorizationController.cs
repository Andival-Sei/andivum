using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

        if (User.Identity?.IsAuthenticated != true ||
            !await HasRegisteredPasskeyAsync(userManager))
        {
            return LoginPageContent(antiforgery);
        }

        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Unauthorized();
        }

        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, subject));

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        principal.SetResources("andivum-api");

        return SignIn(
            principal,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<bool> HasRegisteredPasskeyAsync(
        UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return false;
        }

        var passkeys = await userManager.GetPasskeysAsync(user);
        return AuthPolicy.CanAuthorizeWithPasskey(passkeys.Count);
    }

    private ContentResult LoginPageContent(IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        var token = JsonSerializer.Serialize(tokens.RequestToken);
        return Content(
            LoginPage.Replace(
                "__ANTIFORGERY_TOKEN__",
                token,
                StringComparison.Ordinal),
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

    private const string LoginPage = """
        <!doctype html>
        <html lang="en">
        <head><meta charset="utf-8"><title>Andivum sign in</title></head>
        <body>
          <main>
            <h1>Sign in to Andivum</h1>
            <button id="sign-in" type="button">Continue with passkey</button>
            <h2>New here?</h2>
            <label for="registration-name">Passkey name</label>
            <input id="registration-name" value="My passkey" maxlength="64" />
            <button id="register" type="button">Create an account with passkey</button>
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
            const serializeAnyCredential = (credential) => ({
              authenticatorAttachment: credential.authenticatorAttachment,
              clientExtensionResults: credential.getClientExtensionResults(),
              id: credential.id,
              rawId: toBase64Url(credential.rawId),
              response: credentialResponse(credential),
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
            document.getElementById('register').addEventListener('click', async () => {
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
                    credentialJson: JSON.stringify(serializeAnyCredential(credential)),
                    displayName,
                  }),
                });
                if (!attestationResponse.ok) throw new Error('The passkey was not registered.');
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
