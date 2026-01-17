using Contributions.Models;
using Contributions.Services;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;

namespace Contributions.ViewModels.Pages
{
    public partial class DataViewModel : ObservableObject, INavigationAware
    {
        private readonly GitHubService _gitHubService;
        private bool _isInitialized = false;

        public DataViewModel(GitHubService gitHubService)
        {
            _gitHubService = gitHubService;
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

        public List<string> ThemeModes { get; } = ["Light", "Dark"];

        public List<string> PaletteNames { get; } = Palettes.Select(p => p.Name).ToList();

        public bool CanGenerate => !string.IsNullOrWhiteSpace(Url) && !IsLoading;

        public bool HasResult => ContributionData != null && ContributionData.Contributions.Count > 0;

        public Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
            {
                InitializeViewModel();
            }

            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        private void InitializeViewModel()
        {
            var currentTheme = ApplicationThemeManager.GetAppTheme();
            ThemeMode = currentTheme == ApplicationTheme.Light ? "Light" : "Dark";
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
            var username = GitHubService.CleanUsername(Url);
            if (string.IsNullOrWhiteSpace(username))
            {
                ErrorMessage = "有効なGitHubのURLまたはユーザー名を入力してください";
                return;
            }

            IsLoading = true;
            ErrorMessage = null;
            ContributionData = null;

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
