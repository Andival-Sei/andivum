using Andivum_Windows.Auth;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Andivum_Windows.ViewModels;

/// <summary>
/// Sample ViewModel using CommunityToolkit.Mvvm partial property syntax.
/// Uses <see cref="ObservableProperty"/> for change notification and
/// <see cref="RelayCommand"/> for command binding.
/// </summary>
public partial class MainPageViewModel : ObservableObject
{
    private readonly WindowsAuthClient authClient = new();

    [ObservableProperty]
    public partial string Greeting { get; set; } = ProductInfo.DisplayName;

    [ObservableProperty]
    public partial string SessionStatus { get; set; } = "Not signed in";

    [RelayCommand]
    private async Task SignInAsync()
    {
        SessionStatus = "Opening secure sign-in…";
        try
        {
            await authClient.BeginSignInAsync();
            SessionStatus = "Complete passkey sign-in in the system browser.";
        }
        catch (Exception exception)
        {
            SessionStatus = exception.Message;
        }
    }

    [RelayCommand]
    private void SignOut()
    {
        authClient.SignOut();
        SessionStatus = "Not signed in";
    }

    public async Task HandleCallbackAsync(Uri uri)
    {
        try
        {
            if (await authClient.HandleCallbackAsync(uri))
            {
                SessionStatus = "Signed in with passkey";
            }
        }
        catch (Exception exception)
        {
            SessionStatus = exception.Message;
        }
    }
}
