using Contributions.Resources;
using Contributions.Services;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;

namespace Contributions.ViewModels.Pages
{
    public partial class SettingsViewModel : ObservableObject, INavigationAware
    {
        private bool _isInitialized = false;
        private readonly SettingsService _settingsService;
        private readonly DataViewModel _dataViewModel;
        private bool _isLanguageInitializing;

        public SettingsViewModel(SettingsService settingsService, DataViewModel dataViewModel)
        {
            _settingsService = settingsService;
            _dataViewModel = dataViewModel;
        }

        [ObservableProperty]
        private string _appVersion = String.Empty;

        [ObservableProperty]
        private ApplicationTheme _currentTheme = ApplicationTheme.Unknown;

        [ObservableProperty]
        private bool _autoCopyToClipboard = true;

        public List<LanguageItem> Languages { get; } =
        [
            new LanguageItem(string.Empty, "System (default)"),
            new LanguageItem("en-US", "English"),
            new LanguageItem("ja-JP", "Japanese"),
            new LanguageItem("de-DE", "German"),
            new LanguageItem("es-ES", "Spanish"),
            new LanguageItem("fr-FR", "French"),
            new LanguageItem("hi-IN", "Hindi"),
            new LanguageItem("ko-KR", "Korean"),
            new LanguageItem("zh-Hans", "Chinese (Simplified)")
        ];

        [ObservableProperty]
        private LanguageItem _selectedLanguage = new(string.Empty, "System (default)");

        public async Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
                await InitializeViewModelAsync();

            return;
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        private async Task InitializeViewModelAsync()
        {
            CurrentTheme = ApplicationThemeManager.GetAppTheme();
            AppVersion = GetAssemblyVersion();

            var settings = await _settingsService.LoadAsync();
            AutoCopyToClipboard = settings.AutoCopyToClipboard;
            _dataViewModel.AutoCopyToClipboard = AutoCopyToClipboard;

            Translations.ApplyCulture(settings.Language);

            _isLanguageInitializing = true;
            SelectedLanguage = Languages.FirstOrDefault(l => l.Code == settings.Language)
                ?? Languages[0];
            _isLanguageInitializing = false;

            _isInitialized = true;
        }

        private string GetAssemblyVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? String.Empty;
        }

        [RelayCommand]
        private void OnChangeTheme(string parameter)
        {
            switch (parameter)
            {
                case "theme_light":
                    if (CurrentTheme == ApplicationTheme.Light)
                        break;

                    ApplicationThemeManager.Apply(ApplicationTheme.Light);
                    CurrentTheme = ApplicationTheme.Light;

                    break;

                default:
                    if (CurrentTheme == ApplicationTheme.Dark)
                        break;

                    ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                    CurrentTheme = ApplicationTheme.Dark;

                    break;
            }
        }

        partial void OnAutoCopyToClipboardChanged(bool value)
        {
            _dataViewModel.AutoCopyToClipboard = value;
            _ = _settingsService.SaveAsync(_dataViewModel.CreateSettingsSnapshot());
        }

        partial void OnSelectedLanguageChanged(LanguageItem value)
        {
            if (_isLanguageInitializing)
                return;

            var currentSelection = _dataViewModel.GetCurrentYearSelection();

            Translations.ApplyCulture(value.Code);
            _dataViewModel.Language = value.Code;
            _dataViewModel.RefreshYearOptions(currentSelection.Kind, currentSelection.Year);
            _ = _settingsService.SaveAsync(_dataViewModel.CreateSettingsSnapshot());
        }

        public record LanguageItem(string Code, string DisplayName);
    }
}
