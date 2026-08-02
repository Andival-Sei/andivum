using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace Andivum.Api.Identity;

[ApiController]
public sealed class AuthorizationController : ControllerBase
{
    [HttpGet("~/connect/authorize")]
    public IActionResult Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request is null)
        {
            return BadRequest(new { code = "invalid_authorization_request" });
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            return Content(LoginPage, "text/html; charset=utf-8");
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
            <p id="status" role="status"></p>
          </main>
          <script>
            const status = document.getElementById('status');
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
                const optionsResponse = await fetch('/Account/PasskeyRequestOptions', { method: 'POST' });
                const options = PublicKeyCredential.parseRequestOptionsFromJSON(await optionsResponse.json());
                const credential = await navigator.credentials.get({ publicKey: options });
                const signInResponse = await fetch('/Account/PasskeySignIn', {
                  method: 'POST',
                  headers: { 'Content-Type': 'application/json' },
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
}
