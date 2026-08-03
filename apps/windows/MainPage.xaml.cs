using Microsoft.UI.Xaml.Controls;
using Andivum_Windows.ViewModels;
using Windows.Storage.Pickers;

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

    private async void OnNavigationSelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (ReferenceEquals(args.SelectedItem, FinanceNavigationItem))
        {
            await ViewModel.SelectFinanceAsync();
        }
        else
        {
            ViewModel.SelectOverview();
        }
    }

    private async void OnImportFinanceDocument(
        object sender,
        Microsoft.UI.Xaml.RoutedEventArgs args)
    {
        var picker = new FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
        foreach (var fileType in new[]
                 {
                     ".jpg", ".jpeg", ".png", ".heic", ".webp", ".pdf", ".eml", ".txt", ".csv", ".ofx", ".qfx",
                 })
        {
            picker.FileTypeFilter.Add(fileType);
        }

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        var bytes = await File.ReadAllBytesAsync(file.Path);
        var mimeType = file.ContentType;
        if (string.IsNullOrWhiteSpace(mimeType) || mimeType == "application/octet-stream")
        {
            mimeType = file.FileType.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".heic" => "image/heic",
                ".webp" => "image/webp",
                ".pdf" => "application/pdf",
                ".eml" => "message/rfc822",
                ".csv" => "text/csv",
                _ => "text/plain",
            };
        }

        await ViewModel.ImportDocumentAsync(file.Name, mimeType, bytes);
    }

    private void OnSaveGeminiKey(object sender, Microsoft.UI.Xaml.RoutedEventArgs args)
    {
        ViewModel.SaveGeminiKey(GeminiApiKeyBox.Password);
        GeminiApiKeyBox.Password = string.Empty;
    }
}
