using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using Andivum_Windows.Auth;
using Andivum_Windows.Finance;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;

namespace Andivum_Windows.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly TokenStore tokenStore = new();
    private readonly SupabaseAuthClient authClient;
    private readonly FinanceClient financeClient;
    private readonly GeminiKeyStore geminiKeyStore = new();
    private string financeSource = "manual";
    private string? financeImportFingerprint;

    public MainPageViewModel()
    {
        var configuration = App.LaunchAuthConfiguration
            ?? AuthConfiguration.FromEnvironment("windows", Environment.GetEnvironmentVariable);
        authClient = new SupabaseAuthClient(
            new HttpClient(),
            tokenStore,
            new Uri(configuration.SupabaseUrl),
            configuration.SupabasePublishableKey);
        financeClient = new FinanceClient(
            new HttpClient(),
            tokenStore,
            new Uri(configuration.SupabaseUrl),
            configuration.SupabasePublishableKey);
        FinanceDraftItems.Add(new FinanceDraftItemEditor("Позиция", "0", "other.expense"));
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
    public partial bool IsFinanceBusy { get; set; }

    [ObservableProperty]
    public partial bool IsFinanceSelected { get; set; }

    [ObservableProperty]
    public partial string SessionStatus { get; set; } = UiStrings.Get("AuthStatusNotSignedIn");

    [ObservableProperty]
    public partial string FinanceStatus { get; set; } = "";

    [ObservableProperty]
    public partial string FinanceTitle { get; set; } = "Покупка";

    [ObservableProperty]
    public partial string FinanceTotalAmount { get; set; } = "0";

    [ObservableProperty]
    public partial string FinanceOccurredOn { get; set; } = DateTimeOffset.Now.ToString("yyyy-MM-dd");

    [ObservableProperty]
    public partial string FinanceTransactionType { get; set; } = "expense";

    [ObservableProperty]
    public partial string FinanceAccountId { get; set; } = string.Empty;

    public ObservableCollection<FinanceCategory> FinanceCategories { get; } = [];

    public ObservableCollection<FinanceClient.FinanceAccount> FinanceAccounts { get; } = [];

    public ObservableCollection<FinanceTransaction> FinanceTransactions { get; } = [];

    public ObservableCollection<FinanceDraftItemEditor> FinanceDraftItems { get; } = [];

    public Visibility SignInVisibility => IsSignedIn ? Visibility.Collapsed : Visibility.Visible;

    public Visibility DashboardVisibility => IsSignedIn ? Visibility.Visible : Visibility.Collapsed;

    public Visibility OverviewVisibility => IsFinanceSelected ? Visibility.Collapsed : Visibility.Visible;

    public Visibility FinanceVisibility => IsFinanceSelected ? Visibility.Visible : Visibility.Collapsed;

    public bool IsAuthActionEnabled => !IsBusy;

    public bool IsFinanceActionEnabled => !IsFinanceBusy;

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
            await LoadFinanceAsync();
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
    private async Task SignInAsync() => await RunAuthOperationAsync(
        () => authClient.SignInAsync(Email, Password));

    [RelayCommand]
    private async Task SignUpAsync() => await RunAuthOperationAsync(
        () => authClient.SignUpAsync(Email, Password));

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await authClient.SignOutAsync();
        SetSession(false);
        IsFinanceSelected = false;
        FinanceTransactions.Clear();
        FinanceAccounts.Clear();
        FinanceCategories.Clear();
        SessionStatus = UiStrings.Get("AuthStatusSignedOut");
    }

    [RelayCommand]
    private void OpenAccountSettings() =>
        SessionStatus = UiStrings.Get("AuthStatusSettingsUnavailable");

    [RelayCommand]
    private void AddDraftItem() =>
        FinanceDraftItems.Add(new FinanceDraftItemEditor("Новая позиция", "0", "other.expense", GetAvailableFinanceCategories()));

    public void SelectOverview() => IsFinanceSelected = false;

    public async Task SelectFinanceAsync()
    {
        IsFinanceSelected = true;
        if (FinanceCategories.Count == 0 && !IsFinanceBusy)
        {
            await LoadFinanceAsync();
        }
    }

    [RelayCommand]
    public async Task SaveFinanceTransactionAsync()
    {
        if (IsFinanceBusy || !IsSignedIn)
        {
            return;
        }

        IsFinanceBusy = true;
        FinanceStatus = "Сохраняем операцию…";
        try
        {
            if (string.IsNullOrWhiteSpace(FinanceAccountId))
            {
                throw new InvalidOperationException("Сначала создайте или выберите счёт.");
            }
            var currency = FinanceAccounts.FirstOrDefault(account => account.Id == FinanceAccountId)?.Currency ?? "RUB";
            var items = FinanceDraftItems.Select(item => new FinanceDraftItem(
                item.Name,
                1m,
                FinanceMoney.ParseMinorUnits(item.LineTotalText, currency),
                item.CategorySlug)).ToArray();
            var total = FinanceMoney.ParseMinorUnits(FinanceTotalAmount, currency);
            var draft = new FinanceDraft(
                string.Equals(FinanceTransactionType, "income", StringComparison.OrdinalIgnoreCase)
                    ? Andivum_Windows.Finance.FinanceTransactionType.Income
                    : Andivum_Windows.Finance.FinanceTransactionType.Expense,
                FinanceTitle,
                FinanceOccurredOn,
                currency,
                total,
                items);
            var result = await financeClient.CreateTransactionAsync(
                draft,
                FinanceAccountId,
                financeSource,
                financeImportFingerprint);
            FinanceStatus = result.IsDuplicate
                ? "Такая операция уже есть — дубликат не создан."
                : "Операция сохранена.";
            await LoadFinanceAsync();
        }
        catch (Exception exception)
        {
            FinanceStatus = exception.Message;
        }
        finally
        {
            IsFinanceBusy = false;
        }
    }

    public void SaveGeminiKey(string apiKey)
    {
        try
        {
            geminiKeyStore.Save(apiKey);
            FinanceStatus = "Ключ Gemini сохранён в защищённом хранилище Windows.";
        }
        catch (Exception exception)
        {
            FinanceStatus = exception.Message;
        }
    }

    public async Task ImportDocumentAsync(
        string fileName,
        string mimeType,
        byte[] bytes)
    {
        if (IsFinanceBusy)
        {
            return;
        }

        IsFinanceBusy = true;
        FinanceStatus = $"Разбираем {fileName}…";
        try
        {
            if (bytes.Length == 0 || bytes.Length > 20 * 1024 * 1024)
            {
                FinanceStatus = "Файл должен быть непустым и не больше 20 МБ.";
                return;
            }
            if (!HasSupportedSignature(bytes, mimeType))
            {
                FinanceStatus = "Формат файла не подтверждён. Выберите фото, PDF, письмо или текстовый файл.";
                return;
            }

            var fingerprint = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var key = geminiKeyStore.Read();
            if (key is null)
            {
                if (mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    var ocrText = await WindowsReceiptOcr.ExtractAsync(bytes);
                    if (!FinanceTextImport.TryCreateDraft(ocrText, fileName, FinanceCategories, out var ocrDraft))
                    {
                        FinanceStatus = "OCR завершён, но сумма не найдена. Введите её вручную или сохраните ключ Gemini.";
                        return;
                    }

                    ApplyFinanceDraft(ocrDraft, "ocr", fingerprint);
                    FinanceStatus = "OCR подготовил черновик. Проверьте каждую строку и сохраните вручную.";
                    return;
                }
                if (!mimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) &&
                    !mimeType.Equals("message/rfc822", StringComparison.OrdinalIgnoreCase))
                {
                    FinanceStatus = "Для фото и PDF сохраните Gemini API key в настройках AI.";
                    return;
                }

                var text = Encoding.UTF8.GetString(bytes);
                if (!FinanceTextImport.TryCreateDraft(text, fileName, FinanceCategories, out var textDraft))
                {
                    FinanceStatus = "Текст найден, но в нём не удалось определить сумму. Проверьте данные вручную.";
                    return;
                }
                ApplyFinanceDraft(textDraft, "import", fingerprint);
                FinanceStatus = "Черновик создан из текста. Проверьте его перед сохранением.";
                return;
            }

            var draft = await GeminiReceiptParser.ParseAsync(
                new HttpClient(),
                bytes,
                mimeType,
                FinanceCategories,
                key);
            ApplyFinanceDraft(draft, "ai", fingerprint);
            FinanceStatus = "AI подготовил черновик. Проверьте каждую строку и сохраните вручную.";
        }
        catch (Exception exception)
        {
            FinanceStatus = exception.Message;
        }
        finally
        {
            IsFinanceBusy = false;
        }
    }

    private async Task LoadFinanceAsync()
    {
        if (!IsSignedIn)
        {
            return;
        }

        try
        {
            var categories = await financeClient.GetCategoriesAsync();
            FinanceCategories.Clear();
            foreach (var category in categories)
            {
                FinanceCategories.Add(category);
            }
            foreach (var item in FinanceDraftItems)
            {
                item.CategoryChoices = GetAvailableFinanceCategories();
            }

            var accounts = await financeClient.GetAccountsAsync();
            if (accounts.Count == 0)
            {
                accounts = [await financeClient.CreateAccountAsync("Основной счёт")];
            }
            FinanceAccounts.Clear();
            foreach (var account in accounts)
            {
                FinanceAccounts.Add(account);
            }
            FinanceAccountId = FinanceAccounts.FirstOrDefault()?.Id ?? string.Empty;

            var transactions = await financeClient.GetTransactionsAsync();
            FinanceTransactions.Clear();
            foreach (var transaction in transactions)
            {
                FinanceTransactions.Add(transaction);
            }
            FinanceStatus = FinanceTransactions.Count == 0
                ? "Добавьте первую операцию вручную или импортируйте чек."
                : $"Операций: {FinanceTransactions.Count}";
        }
        catch (Exception exception)
        {
            FinanceStatus = "Финансовые таблицы пока недоступны: " + exception.Message;
        }
    }

    private void ApplyFinanceDraft(
        FinanceDraft draft,
        string source = "manual",
        string? importFingerprint = null)
    {
        financeSource = source;
        financeImportFingerprint = importFingerprint;
        FinanceTitle = draft.Title;
        FinanceTransactionType = draft.Type == Andivum_Windows.Finance.FinanceTransactionType.Income ? "income" : "expense";
        FinanceOccurredOn = draft.OccurredOn;
        FinanceTotalAmount = FormatMajorUnits(draft.TotalMinor, draft.Currency);
        FinanceDraftItems.Clear();
        foreach (var item in draft.Items)
        {
            FinanceDraftItems.Add(new FinanceDraftItemEditor(
                item.Name,
                FormatMajorUnits(item.LineTotalMinor, draft.Currency),
                item.CategorySlug,
                GetAvailableFinanceCategories()));
        }
    }

    private static string FormatMajorUnits(long minor, string currency) =>
        (minor / (currency is "JPY" or "KRW" ? 1m : 100m)).ToString("0.##");

    private static bool HasSupportedSignature(byte[] bytes, string mimeType)
    {
        if (mimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
            mimeType.Equals("message/rfc822", StringComparison.OrdinalIgnoreCase) ||
            mimeType.Contains("ofx", StringComparison.OrdinalIgnoreCase) ||
            mimeType.Contains("qfx", StringComparison.OrdinalIgnoreCase) ||
            mimeType.Equals("application/vnd.ms-outlook", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (mimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return bytes.Length >= 5 && Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-";
        }
        if (mimeType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        }
        if (mimeType.Equals("image/png", StringComparison.OrdinalIgnoreCase))
        {
            return bytes.Length >= 8 &&
                bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
                bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;
        }
        if (mimeType.Equals("image/webp", StringComparison.OrdinalIgnoreCase))
        {
            return bytes.Length >= 12 &&
                Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" &&
                Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP";
        }
        return mimeType.Equals("image/heic", StringComparison.OrdinalIgnoreCase) &&
            bytes.Length >= 12 && Encoding.ASCII.GetString(bytes, 4, 4) == "ftyp";
    }

    private async Task RunAuthOperationAsync(Func<Task<AuthOperationResult>> operation)
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
            await LoadFinanceAsync();
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

    partial void OnIsSignedInChanged(bool value)
    {
        OnPropertyChanged(nameof(SignInVisibility));
        OnPropertyChanged(nameof(DashboardVisibility));
    }

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsAuthActionEnabled));

    partial void OnIsFinanceBusyChanged(bool value) => OnPropertyChanged(nameof(IsFinanceActionEnabled));

    partial void OnIsFinanceSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(OverviewVisibility));
        OnPropertyChanged(nameof(FinanceVisibility));
    }

    partial void OnFinanceTransactionTypeChanged(string value)
    {
        foreach (var item in FinanceDraftItems)
        {
            item.CategoryChoices = GetAvailableFinanceCategories();
            if (!item.CategoryChoices.Any(category =>
                    category.Slug.Equals(item.CategorySlug, StringComparison.OrdinalIgnoreCase)))
            {
                item.CategorySlug = value.Equals("income", StringComparison.OrdinalIgnoreCase)
                    ? "income.other"
                    : "other.expense";
            }
        }
    }

    private IReadOnlyList<FinanceCategory> GetAvailableFinanceCategories()
    {
        var type = FinanceTransactionType.Equals("income", StringComparison.OrdinalIgnoreCase)
            ? "income"
            : "expense";
        return FinanceCategories
            .Where(category => category.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private void SetSession(bool signedIn) => IsSignedIn = new SessionUiState(signedIn).IsSignedIn;
}
