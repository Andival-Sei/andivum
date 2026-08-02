using Microsoft.UI.Xaml.Controls;
using Andivum_Windows.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Andivum_Windows;

/// <summary>
/// The main content page displayed inside the application window.
/// </summary>
public sealed partial class MainPage : Page
{
    public MainPageViewModel ViewModel { get; } = new();

    public MainPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs args)
    {
        Loaded -= OnLoaded;
        await ViewModel.InitializeAsync();
    }

    private void OnPasswordChanged(object sender, Microsoft.UI.Xaml.RoutedEventArgs args)
    {
        if (sender is Microsoft.UI.Xaml.Controls.PasswordBox passwordBox)
        {
            ViewModel.Password = passwordBox.Password;
        }
    }
}
