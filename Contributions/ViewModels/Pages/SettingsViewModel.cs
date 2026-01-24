using Contributions.Resources;
using Contributions.Services;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;

namespace Contributions.ViewModels.Pages
{
    /// <summary>
    /// 設定画面の状態と操作を管理するViewModel。
    /// </summary>
    public partial class SettingsViewModel : ObservableObject, INavigationAware
    {
        private bool _isInitialized = false;
        private readonly SettingsService _settingsService;
        private readonly DataViewModel _dataViewModel;
        private bool _isLanguageInitializing;
        private bool _isShareSettingsInitializing;

        /// <summary>
        /// SettingsViewModelを生成する。
        /// </summary>
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

        [ObservableProperty]
        private string _shareText = DataViewModel.DefaultShareText;

        [ObservableProperty]
        private ShareUrlOptionItem _selectedShareUrlOption =
            new(DataViewModel.ShareUrlOptionGitHub, "GitHub");

        [ObservableProperty]
        private string _shareHashtag1 = string.Empty;

        [ObservableProperty]
        private string _shareHashtag2 = string.Empty;

        [ObservableProperty]
        private string _shareHashtag3 = string.Empty;

        [ObservableProperty]
        private List<ShareUrlOptionItem> _shareUrlOptions = CreateShareUrlOptions();

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

        /// <summary>
        /// ナビゲーション時に初期化を行う。
        /// </summary>
        public async Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
                await InitializeViewModelAsync();

            return;
        }

        /// <summary>
        /// ナビゲーション離脱時の処理。
        /// </summary>
        public async Task OnNavigatedFromAsync()
        {
            await _settingsService.SaveAsync(_dataViewModel.CreateSettingsSnapshot());
        }

        /// <summary>
        /// 設定画面の初期状態を読み込む。
        /// </summary>
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

            _isShareSettingsInitializing = true;
            ShareText = settings.ShareText ?? DataViewModel.DefaultShareText;
            ShareUrlOptions = CreateShareUrlOptions();
            SelectedShareUrlOption = ShareUrlOptions.FirstOrDefault(option =>
                    option.Value == DataViewModel.ResolveShareUrlOption(settings))
                ?? ShareUrlOptions[1];
            ShareHashtag1 = DataViewModel.ResolveShareHashtag1(settings);
            ShareHashtag2 = settings.ShareHashtag2 ?? string.Empty;
            ShareHashtag3 = settings.ShareHashtag3 ?? string.Empty;
            _isShareSettingsInitializing = false;
            _dataViewModel.ShareText = ShareText;
            _dataViewModel.ShareUrlOption = SelectedShareUrlOption.Value;
            _dataViewModel.ShareHashtag1 = ShareHashtag1;
            _dataViewModel.ShareHashtag2 = ShareHashtag2;
            _dataViewModel.ShareHashtag3 = ShareHashtag3;

            _isInitialized = true;
        }

        /// <summary>
        /// 実行中アセンブリのバージョン文字列を取得する。
        /// </summary>
        private string GetAssemblyVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? String.Empty;
        }

        /// <summary>
        /// テーマを切り替える。
        /// </summary>
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

        /// <summary>
        /// 自動コピー設定の変更を反映する。
        /// </summary>
        partial void OnAutoCopyToClipboardChanged(bool value)
        {
            _dataViewModel.AutoCopyToClipboard = value;
            _ = _settingsService.SaveAsync(_dataViewModel.CreateSettingsSnapshot());
        }

        /// <summary>
        /// 共有テキストの変更を反映する。
        /// </summary>
        partial void OnShareTextChanged(string value)
        {
            if (_isShareSettingsInitializing)
                return;

            _dataViewModel.ShareText = value;
            _ = _settingsService.SaveAsync(_dataViewModel.CreateSettingsSnapshot());
        }

        /// <summary>
        /// 共有URLの変更を反映する。
        /// </summary>
        partial void OnSelectedShareUrlOptionChanged(ShareUrlOptionItem value)
        {
            if (_isShareSettingsInitializing)
                return;

            _dataViewModel.ShareUrlOption = value.Value;
            _ = _settingsService.SaveAsync(_dataViewModel.CreateSettingsSnapshot());
        }

        /// <summary>
        /// ハッシュタグ1の変更を反映する。
        /// </summary>
        partial void OnShareHashtag1Changed(string value)
        {
            if (_isShareSettingsInitializing)
                return;

            _dataViewModel.ShareHashtag1 = value;
            _ = _settingsService.SaveAsync(_dataViewModel.CreateSettingsSnapshot());
        }

        /// <summary>
        /// ハッシュタグ2の変更を反映する。
        /// </summary>
        partial void OnShareHashtag2Changed(string value)
        {
            if (_isShareSettingsInitializing)
                return;

            _dataViewModel.ShareHashtag2 = value;
            _ = _settingsService.SaveAsync(_dataViewModel.CreateSettingsSnapshot());
        }

        /// <summary>
        /// ハッシュタグ3の変更を反映する。
        /// </summary>
        partial void OnShareHashtag3Changed(string value)
        {
            if (_isShareSettingsInitializing)
                return;

            _dataViewModel.ShareHashtag3 = value;
            _ = _settingsService.SaveAsync(_dataViewModel.CreateSettingsSnapshot());
        }

        /// <summary>
        /// 言語変更を反映し、年の表示ラベルも更新する。
        /// </summary>
        partial void OnSelectedLanguageChanged(LanguageItem value)
        {
            if (_isLanguageInitializing)
                return;

            var currentSelection = _dataViewModel.GetCurrentYearSelection();

            Translations.ApplyCulture(value.Code);
            _dataViewModel.Language = value.Code;
            _dataViewModel.RefreshYearOptions(currentSelection.Kind, currentSelection.Year);
            RefreshShareUrlOptions();
            _ = _settingsService.SaveAsync(_dataViewModel.CreateSettingsSnapshot());
        }

        /// <summary>
        /// 言語選択肢の表示情報。
        /// </summary>
        public record LanguageItem(string Code, string DisplayName);

        /// <summary>
        /// 共有URLの選択肢。
        /// </summary>
        public record ShareUrlOptionItem(string Value, string DisplayName);

        private static List<ShareUrlOptionItem> CreateShareUrlOptions()
        {
            return
            [
                new ShareUrlOptionItem(
                    DataViewModel.ShareUrlOptionNone,
                    Translations.GetString("ShareUrlOption_None")),
                new ShareUrlOptionItem(
                    DataViewModel.ShareUrlOptionGitHub,
                    Translations.GetString("ShareUrlOption_GitHub"))
            ];
        }

        private void RefreshShareUrlOptions()
        {
            _isShareSettingsInitializing = true;
            var selectedValue = SelectedShareUrlOption?.Value;
            ShareUrlOptions = CreateShareUrlOptions();
            SelectedShareUrlOption = ShareUrlOptions.FirstOrDefault(option =>
                    option.Value == selectedValue)
                ?? ShareUrlOptions[1];
            _isShareSettingsInitializing = false;
        }
    }
}
