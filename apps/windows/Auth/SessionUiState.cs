namespace Andivum_Windows.Auth;

public enum AuthShellScreen
{
    SignIn,
    Dashboard,
}

public readonly record struct SessionUiState(bool IsSignedIn)
{
    public AuthShellScreen Screen => IsSignedIn
        ? AuthShellScreen.Dashboard
        : AuthShellScreen.SignIn;
}
