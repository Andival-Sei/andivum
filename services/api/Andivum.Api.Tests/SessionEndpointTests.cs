using System.Security.Claims;
using Andivum.Api.Identity;
using OpenIddict.Abstractions;
using Xunit;

namespace Andivum.Api.Tests;

public sealed class SessionEndpointTests
{
    [Fact]
    public void Current_session_uses_the_stable_subject_as_account_id()
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(OpenIddictConstants.Claims.Subject, "account-123"),
            ],
            authenticationType: "Bearer"));

        var response = SessionEndpoint.CreateResponse(principal);

        Assert.Equal("account-123", response.UserId);
        Assert.True(response.Authenticated);
    }
}
