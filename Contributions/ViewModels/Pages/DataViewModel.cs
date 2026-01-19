using Contributions.Models;
using Contributions.Services;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;

namespace Contributions.ViewModels.Pages
{
    public partial class DataViewModel : ObservableObject, INavigationAware
    {
        private readonly GitHubService _gitHubService;
        private readonly SettingsService _settingsService;
        private bool _isInitialized = false;
        private bool _isLoadingSettings = false;
        public const string DefaultYearOption = "デフォルト";
        public const string AllYearsOption = "すべて";

        public DataViewModel(GitHubService gitHubService, SettingsService settingsService)
        {
            _gitHubService = gitHubService;
            _settingsService = settingsService;
        }

        [ObservableProperty]
        private string _url = string.Empty;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private ContributionData? _contributionData;

        [ObservableProperty]
        private string _themeMode = "Dark";

        [ObservableProperty]
        private string _paletteName = "standard";

        [ObservableProperty]
        private List<string> _availableYears = [DefaultYearOption, AllYearsOption];

        [ObservableProperty]
        private string _selectedYear = DefaultYearOption;

        private bool _canShareToX;

        public bool CanShareToX
        {
            get => _canShareToX;
            set => SetProperty(ref _canShareToX, value);
        }

        public List<string> ThemeModes { get; } = ["Light", "Dark"];

        public List<PaletteItem> PaletteItems { get; } = Palettes
            .Select(p => new PaletteItem(p.Name, p.Grades, p.Grades))
            .ToList();

        public bool CanGenerate => !string.IsNullOrWhiteSpace(Url) && !IsLoading;

        public bool HasResult => ContributionData != null && ContributionData.Contributions.Count > 0;

        public async Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
            {
                await InitializeViewModelAsync();
            }
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        private async Task InitializeViewModelAsync()
        {
            var currentTheme = ApplicationThemeManager.GetAppTheme();
            ThemeMode = currentTheme == ApplicationTheme.Light ? "Light" : "Dark";
            _isLoadingSettings = true;

            try
            {
                var settings = await _settingsService.LoadAsync();
                if (!string.IsNullOrWhiteSpace(settings.ThemeMode))
                    ThemeMode = settings.ThemeMode;
                if (!string.IsNullOrWhiteSpace(settings.PaletteName))
                    PaletteName = settings.PaletteName;
                if (!string.IsNullOrWhiteSpace(settings.Url))
                    Url = settings.Url;
            }
            finally
            {
                _isLoadingSettings = false;
            }

            if (!string.IsNullOrWhiteSpace(Url))
                await GenerateAsync();

            _isInitialized = true;
        }

        partial void OnUrlChanged(string value)
        {
            OnPropertyChanged(nameof(CanGenerate));
        }

        partial void OnIsLoadingChanged(bool value)
        {
            OnPropertyChanged(nameof(CanGenerate));
        }

        partial void OnContributionDataChanged(ContributionData? value)
        {
            OnPropertyChanged(nameof(HasResult));
            CanShareToX = false;
            UpdateYearOptions(value);
        }

        partial void OnSelectedYearChanged(string value)
        {
            CanShareToX = false;
        }

        partial void OnThemeModeChanged(string value)
        {
            if (value == "Light")
                ApplicationThemeManager.Apply(ApplicationTheme.Light);
            else
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
        }

        [RelayCommand]
        private async Task GenerateAsync()
        {
            ErrorMessage = null;
            ContributionData = null;

            var input = Url?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                ErrorMessage = "有効なGitHubのURLまたはユーザー名を入力してください";
                return;
            }

            string username;
            if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
            {
                if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
                {
                    ErrorMessage = "URLは https://github.com/ で始まるGitHubのURLを入力してください";
                    return;
                }

                var path = uri.AbsolutePath.Trim('/');
                if (string.IsNullOrWhiteSpace(path))
                {
                    ErrorMessage = "URLにユーザー名を含めてください";
                    return;
                }

                username = path.Split('/')[0];
            }
            else if (input.StartsWith("github.com/", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = "URLは https://github.com/ で始まるGitHubのURLを入力してください";
                return;
            }
            else
            {
                username = GitHubService.CleanUsername(input);
                if (string.IsNullOrWhiteSpace(username))
                {
                    ErrorMessage = "有効なGitHubのURLまたはユーザー名を入力してください";
                    return;
                }
            }

            IsLoading = true;

            try
            {
                var data = await _gitHubService.FetchDataForAllYearsAsync(username);
                if (data.Years.Count == 0)
                {
                    ErrorMessage = "プロフィールが見つかりませんでした";
                }
                else
                {
                    ContributionData = data;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"プロフィールの取得に失敗しました: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public UserSettings CreateSettingsSnapshot()
        {
            return new UserSettings
            {
                Url = Url,
                ThemeMode = ThemeMode,
                PaletteName = PaletteName
            };
        }

        public (string Background, string Text, string SubText) GetThemeColors()
        {
            return ThemeMode == "Dark"
                ? ("#0d1117", "#c9d1d9", "#8b949e")
                : ("#ffffff", "#24292f", "#57606a");
        }

        public string[] GetPaletteColors()
        {
            var palette = Palettes.FirstOrDefault(p => p.Name == PaletteName) ?? Palettes[0];
            return palette.Grades;
        }

        public List<YearData> GetOrderedYears()
        {
            if (ContributionData == null || ContributionData.Years.Count == 0)
                return [];

            return ContributionData.Years;
        }

        private void UpdateYearOptions(ContributionData? data)
        {
            var options = new List<string> { DefaultYearOption, AllYearsOption };
            if (data != null && data.Years.Count > 0)
                options.AddRange(data.Years.Select(y => y.Year?.Trim()).Where(y => !string.IsNullOrWhiteSpace(y))!);

            AvailableYears = options;
            SelectedYear = DefaultYearOption;
        }

        public record PaletteItem(string Name, string[] Grades, string[] DisplayGrades);

        private record PaletteDefinition(string Name, string[] Grades);

        private static readonly List<PaletteDefinition> Palettes =
        [
            new PaletteDefinition(
                "standard",
                ["#ebedf0", "#9be9a8", "#40c463", "#30a14e", "#216e39"]
            ),
            new PaletteDefinition(
                "classic",
                ["#ebedf0", "#c6e48b", "#7bc96f", "#239a3b", "#196127"]
            ),
            new PaletteDefinition(
                "githubDark",
                ["#161b22", "#003820", "#00602d", "#10983d", "#27d545"]
            ),
            new PaletteDefinition(
                "halloween",
                ["#ebedf0", "#FFEE4A", "#FFC501", "#FE9600", "#03001C"]
            ),
            new PaletteDefinition(
                "teal",
                ["#ebedf0", "#7FFFD4", "#76EEC6", "#66CDAA", "#458B74"]
            ),
            new PaletteDefinition(
                "leftPad",
                ["#2F2F2F", "#646464", "#A5A5A5", "#DDDDDD", "#F6F6F6"]
            ),
            new PaletteDefinition(
                "dracula",
                ["#282a36", "#44475a", "#6272a4", "#bd93f9", "#ff79c6"]
            ),
            new PaletteDefinition(
                "blue",
                ["#222222", "#263342", "#344E6C", "#416895", "#4F83BF"]
            ),
            new PaletteDefinition(
                "panda",
                ["#242526", "#34353B", "#6FC1FF", "#19f9d8", "#FF4B82"]
            ),
            new PaletteDefinition(
                "sunny",
                ["#fff9ae", "#f8ed62", "#e9d700", "#dab600", "#a98600"]
            ),
            new PaletteDefinition(
                "pink",
                ["#ebedf0", "#e48bdc", "#ca5bcc", "#a74aa8", "#61185f"]
            ),
            new PaletteDefinition(
                "YlGnBu",
                ["#ebedf0", "#a1dab4", "#41b6c4", "#2c7fb8", "#253494"]
            ),
            new PaletteDefinition(
                "solarizedDark",
                ["#073642", "#268bd2", "#2aa198", "#b58900", "#d33682"]
            ),
            new PaletteDefinition(
                "solarizedLight",
                ["#eee8d5", "#b58900", "#cb4b16", "#dc322f", "#6c71c4"]
            )
        ];
    }
}
