using Andivum_Windows.Auth;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;

namespace Andivum_Windows.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly SupabaseAuthClient authClient = CreateAuthClient();

    private static SupabaseAuthClient CreateAuthClient()
    {
        var configuration = App.LaunchAuthConfiguration
            ?? AuthConfiguration.FromEnvironment(
                "windows",
                Environment.GetEnvironmentVariable);
        return new SupabaseAuthClient(
            new HttpClient(),
            new TokenStore(),
            new Uri(configuration.SupabaseUrl),
            configuration.SupabasePublishableKey);
    }

    [ObservableProperty]
    public partial string Greeting { get; set; } = ProductInfo.DisplayName;

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSignedIn { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string SessionStatus { get; set; } = UiStrings.Get("AuthStatusNotSignedIn");

    public Visibility SignInVisibility => IsSignedIn
        ? Visibility.Collapsed
        : Visibility.Visible;

    public Visibility DashboardVisibility => IsSignedIn
        ? Visibility.Visible
        : Visibility.Collapsed;

    public bool IsAuthActionEnabled => !IsBusy;

    public MainPageViewModel()
    {
        SetSession(false);
    }

    public async Task InitializeAsync()
    {
        if (authClient.CurrentSession is null)
        {
            SetSession(false);
            return;
        }

        IsBusy = true;
        SessionStatus = UiStrings.Get("AuthStatusChecking");
        try
        {
            await authClient.GetCurrentSessionAsync();
            SetSession(true);
            SessionStatus = UiStrings.Get("AuthStatusVerified");
        }
        catch
        {
            SetSession(false);
            SessionStatus = UiStrings.Get("AuthStatusSessionUnavailable");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        await RunAuthOperationAsync(
            () => authClient.SignInAsync(Email, Password));
    }

    [RelayCommand]
    private async Task SignUpAsync()
    {
        await RunAuthOperationAsync(
            () => authClient.SignUpAsync(Email, Password));
    }

    private async Task RunAuthOperationAsync(
        Func<Task<AuthOperationResult>> operation)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        SessionStatus = UiStrings.Get("AuthStatusWorking");
        try
        {
            var result = await operation();
            if (!result.SessionCreated)
            {
                SetSession(false);
                SessionStatus = UiStrings.Get("AuthStatusEmailConfirmation");
                return;
            }

            await authClient.GetCurrentSessionAsync();
            SetSession(true);
            SessionStatus = UiStrings.Get("AuthStatusVerified");
        }
        catch (Exception exception)
        {
            SetSession(false);
            SessionStatus = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await authClient.SignOutAsync();
        SetSession(false);
        SessionStatus = UiStrings.Get("AuthStatusSignedOut");
    }

    [RelayCommand]
    private void OpenAccountSettings()
    {
        SessionStatus = UiStrings.Get("AuthStatusSettingsUnavailable");
    }

    partial void OnIsSignedInChanged(bool value)
    {
        OnPropertyChanged(nameof(SignInVisibility));
        OnPropertyChanged(nameof(DashboardVisibility));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsAuthActionEnabled));
    }

    private void SetSession(bool signedIn)
    {
        IsSignedIn = new SessionUiState(signedIn).IsSignedIn;
    }
}
