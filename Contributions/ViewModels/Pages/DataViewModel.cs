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
        private string _paletteName = "GitHub";

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
            return ThemeMode == "Dark" ? palette.Dark : palette.Light;
        }

        private record Palette(string Name, string[] Light, string[] Dark);

        private static readonly List<Palette> Palettes =
        [
            new Palette(
                "GitHub",
                ["#ebedf0", "#9be9a8", "#40c463", "#30a14e", "#216e39"],
                ["#161b22", "#0e4429", "#006d32", "#26a641", "#39d353"]
            ),
            new Palette(
                "Halloween",
                ["#ebedf0", "#ffe29a", "#ffc400", "#ff8c00", "#ff5a00"],
                ["#161b22", "#4a2b0f", "#7a3f00", "#b45309", "#f97316"]
            ),
            new Palette(
                "Ocean",
                ["#ebedf0", "#b3d8ff", "#64b5f6", "#1e88e5", "#0d47a1"],
                ["#161b22", "#0f2747", "#0b4f6c", "#1368aa", "#1c77c3"]
            ),
            new Palette(
                "Dracula",
                ["#ebedf0", "#f1a2e6", "#bd93f9", "#8be9fd", "#50fa7b"],
                ["#161b22", "#4a2b4f", "#6a4c93", "#3aa7bd", "#2ea043"]
            )
        ];
    }
}
