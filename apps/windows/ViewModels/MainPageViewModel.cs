using Andivum_Windows.Auth;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;

namespace Andivum_Windows.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly WindowsAuthClient authClient = new();

    [ObservableProperty]
    public partial string Greeting { get; set; } = ProductInfo.DisplayName;

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

    public bool IsSignInEnabled => !IsBusy;

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
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        SessionStatus = UiStrings.Get("AuthStatusOpening");
        try
        {
            await authClient.BeginSignInAsync();
            SessionStatus = UiStrings.Get("AuthStatusBrowser");
        }
        catch (Exception exception)
        {
            SessionStatus = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SignOut()
    {
        authClient.SignOut();
        SetSession(false);
        SessionStatus = UiStrings.Get("AuthStatusSignedOut");
    }

    public async Task HandleCallbackAsync(Uri uri)
    {
        try
        {
            if (await authClient.HandleCallbackAsync(uri))
            {
                await authClient.GetCurrentSessionAsync();
                SetSession(true);
                SessionStatus = UiStrings.Get("AuthStatusVerified");
            }
        }
        catch (Exception exception)
        {
            SetSession(authClient.CurrentSession is not null);
            SessionStatus = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnIsSignedInChanged(bool value)
    {
        OnPropertyChanged(nameof(SignInVisibility));
        OnPropertyChanged(nameof(DashboardVisibility));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsSignInEnabled));
    }

    private void SetSession(bool signedIn)
    {
        IsSignedIn = new SessionUiState(signedIn).IsSignedIn;
    }
}
