using System.Security.Claims;
using OpenIddict.Abstractions;

namespace Andivum.Api.Identity;

public sealed record SessionResponse(string UserId, bool Authenticated);

public static class SessionEndpoint
{
    public static SessionResponse CreateResponse(ClaimsPrincipal principal)
    {
        var userId = principal.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException(
                "The authenticated principal does not contain a subject claim.");
        }

        return new SessionResponse(userId, Authenticated: true);
    }
}
