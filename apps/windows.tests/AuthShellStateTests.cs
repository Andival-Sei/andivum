using Andivum_Windows.Auth;
using Xunit;

namespace Andivum_Windows.Tests;

public sealed class AuthShellStateTests
{
    [Fact]
    public void An_existing_session_opens_the_dashboard_screen()
    {
        Assert.Equal(AuthShellScreen.Dashboard, new SessionUiState(true).Screen);
    }

    [Fact]
    public void A_signed_out_session_opens_the_sign_in_screen()
    {
        Assert.Equal(AuthShellScreen.SignIn, new SessionUiState(false).Screen);
    }
}
