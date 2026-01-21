using Contributions.Models;
using Contributions.Resources;
using Contributions.Services;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;

namespace Contributions.ViewModels.Pages
{
    public partial class DataViewModel : ObservableObject, INavigationAware
    {
        private readonly GitHubService _gitHubService;
        private readonly SettingsService _settingsService;
        private readonly ContributionCacheService _cacheService;
        private bool _isInitialized = false;
        private bool _isLoadingSettings = false;
        private bool _isLoadingYearData = false;
        private string? _currentUsername;
        private List<GitHubService.YearRange> _availableYearRanges = [];
        private readonly Dictionary<string, YearData> _yearDataByYear = new();
        private List<Contribution> _defaultContributions = [];
        private YearOptionKind? _preferredYearKind;
        private string? _preferredYearValue;
        public static string DefaultYearOption => Translations.GetString("YearOption_Default");
        public static string AllYearsOption => Translations.GetString("YearOption_All");

        public enum YearOptionKind
        {
            Default,
            All,
            Year
        }

        private static readonly HashSet<string> DefaultLabels =
        [
            "Default", "デフォルト", "Standard", "Predeterminado", "Par défaut", "डिफ़ॉल्ट", "기본", "默认"
        ];

        private static readonly HashSet<string> AllLabels =
        [
            "All", "すべて", "Alle", "Todos", "Tous", "सभी", "전체", "全部"
        ];

        public DataViewModel(
            GitHubService gitHubService,
            SettingsService settingsService,
            ContributionCacheService cacheService)
        {
            _gitHubService = gitHubService;
            _settingsService = settingsService;
            _cacheService = cacheService;
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
        private bool _autoCopyToClipboard = true;

        [ObservableProperty]
        private string _copyButtonText = "Copy to Clipboard";

        [ObservableProperty]
        private string _language = string.Empty;

        [ObservableProperty]
        private List<string> _availableYears = [DefaultYearOption, AllYearsOption];

        [ObservableProperty]
        private string _selectedYear = DefaultYearOption;

        [ObservableProperty]
        private bool _isManualGenerateRequested;

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
                Language = settings.Language;
                (_preferredYearKind, _preferredYearValue) = GetPreferredYearSelection(settings);
                AutoCopyToClipboard = settings.AutoCopyToClipboard;

                Translations.ApplyCulture(Language);
                ApplySavedYearSelection();
            }
            finally
            {
                _isLoadingSettings = false;
            }

            if (!string.IsNullOrWhiteSpace(Url))
                await LoadDataAsync(isManual: false, forceRefresh: false);

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
            CanShareToX = HasResult;
            UpdateYearOptions(value, _availableYearRanges, _preferredYearKind, _preferredYearValue);
            _preferredYearKind = null;
            _preferredYearValue = null;
        }

        partial void OnSelectedYearChanged(string value)
        {
            CanShareToX = HasResult;

            if (_isLoadingSettings || IsLoading)
                return;

            _ = _settingsService.SaveAsync(CreateSettingsSnapshot());

            if (string.IsNullOrWhiteSpace(_currentUsername) || _availableYearRanges.Count == 0)
                return;

            _ = LoadYearDataIfNeededAsync();
        }

        partial void OnThemeModeChanged(string value)
        {
            if (value == "Light")
                ApplicationThemeManager.Apply(ApplicationTheme.Light);
            else
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
        }

        partial void OnAutoCopyToClipboardChanged(bool value)
        {
            CopyButtonText = value ? "Copy again" : "Copy to Clipboard";
        }

        [RelayCommand]
        private async Task GenerateAsync()
        {
            await GenerateCoreAsync(isManual: true);
        }

        private async Task GenerateCoreAsync(bool isManual)
        {
            if (isManual)
            {
                _preferredYearKind = GetCurrentYearSelection().Kind;
                _preferredYearValue = GetCurrentYearSelection().Year;
            }

            await LoadDataAsync(isManual, forceRefresh: isManual);
        }

        private async Task LoadDataAsync(bool isManual, bool forceRefresh)
        {
            ErrorMessage = null;
            ContributionData = null;
            _yearDataByYear.Clear();
            _availableYearRanges = [];
            _defaultContributions = [];

            var input = Url?.Trim() ?? string.Empty;
            if (!TryResolveUsername(input, out var username, out var errorMessage))
            {
                ErrorMessage = errorMessage;
                return;
            }

            _currentUsername = username;
            if (isManual)
                IsManualGenerateRequested = true;

            IsLoading = true;
            try
            {
                _availableYearRanges = await _gitHubService.FetchYearsAsync(username);
                RefreshYearOptions(GetCurrentYearSelection().Kind, GetCurrentYearSelection().Year);

                await EnsureDefaultContributionsAsync(username, forceRefresh);
                UpdateContributionData();

                var selection = GetCurrentYearSelection();
                await EnsureYearDataLoadedAsync(selection.Kind, selection.Year, forceRefresh);
                UpdateContributionData();

                if (_availableYearRanges.Count == 0)
                {
                    ErrorMessage = "プロフィールが見つかりませんでした";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"プロフィールの取得に失敗しました: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                if (isManual)
                    IsManualGenerateRequested = false;
            }
        }

        private async Task LoadYearDataIfNeededAsync()
        {
            if (_isLoadingYearData)
                return;

            var selection = GetCurrentYearSelection();
            if (selection.Kind == null)
                return;

            if (!NeedsYearDataLoad(selection.Kind, selection.Year))
                return;

            _isLoadingYearData = true;
            IsLoading = true;

            try
            {
                await EnsureYearDataLoadedAsync(selection.Kind, selection.Year, forceRefresh: false);
                UpdateContributionData();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"プロフィールの取得に失敗しました: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                _isLoadingYearData = false;
            }
        }

        private bool NeedsYearDataLoad(YearOptionKind? kind, string? year)
        {
            if (kind == null)
                return false;

            if (kind == YearOptionKind.Default)
                return false;

            if (kind == YearOptionKind.All)
                return _availableYearRanges.Any(range => !_yearDataByYear.ContainsKey(range.Year));

            if (kind == YearOptionKind.Year)
            {
                if (string.IsNullOrWhiteSpace(year))
                    return false;

                return !_yearDataByYear.ContainsKey(year);
            }

            return false;
        }

        private async Task EnsureDefaultContributionsAsync(string username, bool forceRefresh)
        {
            if (!forceRefresh)
            {
                var cached = await _cacheService.LoadDefaultContributionsAsync(username);
                if (cached != null)
                {
                    _defaultContributions = cached;
                    return;
                }
            }

            var fetched = await _gitHubService.FetchDefaultContributionsAsync(username);
            _defaultContributions = fetched;
            await _cacheService.SaveDefaultContributionsAsync(username, fetched);
        }

        private async Task EnsureYearDataLoadedAsync(YearOptionKind? kind, string? year, bool forceRefresh)
        {
            if (kind == null || string.IsNullOrWhiteSpace(_currentUsername))
                return;

            if (kind == YearOptionKind.Year)
            {
                if (string.IsNullOrWhiteSpace(year))
                    return;

                var range = _availableYearRanges.FirstOrDefault(r => r.Year == year);
                if (range == null)
                    return;

                if (!forceRefresh)
                {
                    var cached = await _cacheService.LoadYearDataAsync(_currentUsername, year);
                    if (cached != null)
                    {
                        _yearDataByYear[year] = cached;
                        return;
                    }
                }

                var fetched = await _gitHubService.FetchYearDataAsync(_currentUsername, range);
                _yearDataByYear[year] = fetched;
                await _cacheService.SaveYearDataAsync(_currentUsername, fetched);
                return;
            }

            if (kind == YearOptionKind.All)
            {
                foreach (var range in _availableYearRanges)
                {
                    if (!forceRefresh && _yearDataByYear.ContainsKey(range.Year))
                        continue;

                    if (!forceRefresh)
                    {
                        var cached = await _cacheService.LoadYearDataAsync(_currentUsername, range.Year);
                        if (cached != null)
                        {
                            _yearDataByYear[range.Year] = cached;
                            continue;
                        }
                    }

                    var fetched = await _gitHubService.FetchYearDataAsync(_currentUsername, range);
                    _yearDataByYear[range.Year] = fetched;
                    await _cacheService.SaveYearDataAsync(_currentUsername, fetched);
                }
            }
        }

        private void UpdateContributionData()
        {
            var yearData = _yearDataByYear.Values
                .Where(y => y.Contributions.Count > 0)
                .OrderByDescending(y => y.Year)
                .ToList();

            var contributions = yearData
                .SelectMany(y => y.Contributions)
                .OrderByDescending(c => c.Date)
                .ToList();

            if (contributions.Count == 0 && _defaultContributions.Count > 0)
                contributions = _defaultContributions.OrderByDescending(c => c.Date).ToList();

            ContributionData = new ContributionData
            {
                Years = yearData,
                Contributions = contributions,
                DefaultContributions = _defaultContributions
            };
        }

        private static bool TryResolveUsername(
            string input,
            out string username,
            out string? errorMessage)
        {
            username = string.Empty;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                errorMessage = "有効なGitHubのURLまたはユーザー名を入力してください";
                return false;
            }

            if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
            {
                if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = "URLは https://github.com/ で始まるGitHubのURLを入力してください";
                    return false;
                }

                var path = uri.AbsolutePath.Trim('/');
                if (string.IsNullOrWhiteSpace(path))
                {
                    errorMessage = "URLにユーザー名を含めてください";
                    return false;
                }

                username = path.Split('/')[0];
                return true;
            }

            if (input.StartsWith("github.com/", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "URLは https://github.com/ で始まるGitHubのURLを入力してください";
                return false;
            }

            username = GitHubService.CleanUsername(input);
            if (string.IsNullOrWhiteSpace(username))
            {
                errorMessage = "有効なGitHubのURLまたはユーザー名を入力してください";
                return false;
            }

            return true;
        }

        public UserSettings CreateSettingsSnapshot()
        {
            return new UserSettings
            {
                Url = Url,
                ThemeMode = ThemeMode,
                PaletteName = PaletteName,
                AutoCopyToClipboard = AutoCopyToClipboard,
                Language = Language,
                SelectedYear = SelectedYear,
                SelectedYearKind = GetCurrentYearSelection().Kind?.ToString().ToLowerInvariant() ?? string.Empty,
                SelectedYearValue = GetCurrentYearSelection().Year ?? string.Empty
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

        private void UpdateYearOptions(
            ContributionData? data,
            List<GitHubService.YearRange>? yearRanges,
            YearOptionKind? preferredKind,
            string? preferredYear)
        {
            var options = new List<string> { DefaultYearOption, AllYearsOption };
            if (yearRanges != null && yearRanges.Count > 0)
            {
                options.AddRange(yearRanges.Select(y => y.Year?.Trim()).Where(y => !string.IsNullOrWhiteSpace(y))!);
            }
            else if (data != null && data.Years.Count > 0)
            {
                options.AddRange(data.Years.Select(y => y.Year?.Trim()).Where(y => !string.IsNullOrWhiteSpace(y))!);
            }

            AvailableYears = options;
            if (preferredKind == YearOptionKind.All)
            {
                SelectedYear = AllYearsOption;
                return;
            }

            if (preferredKind == YearOptionKind.Default)
            {
                SelectedYear = DefaultYearOption;
                return;
            }

            if (preferredKind == YearOptionKind.Year
                && !string.IsNullOrWhiteSpace(preferredYear)
                && options.Contains(preferredYear))
            {
                SelectedYear = preferredYear;
                return;
            }

            SelectedYear = DefaultYearOption;
        }

        public void RefreshYearOptions(YearOptionKind? preferredKind, string? preferredYear)
        {
            UpdateYearOptions(ContributionData, _availableYearRanges, preferredKind, preferredYear);
        }

        private void ApplySavedYearSelection()
        {
            var options = new List<string> { DefaultYearOption, AllYearsOption };
            if (_preferredYearKind == YearOptionKind.Year
                && !string.IsNullOrWhiteSpace(_preferredYearValue)
                && !options.Contains(_preferredYearValue))
            {
                options.Add(_preferredYearValue);
            }

            AvailableYears = options;

            if (_preferredYearKind == YearOptionKind.All)
                SelectedYear = AllYearsOption;
            else if (_preferredYearKind == YearOptionKind.Default)
                SelectedYear = DefaultYearOption;
            else if (_preferredYearKind == YearOptionKind.Year
                && !string.IsNullOrWhiteSpace(_preferredYearValue))
                SelectedYear = _preferredYearValue;
            else
                SelectedYear = DefaultYearOption;
        }

        public (YearOptionKind? Kind, string? Year) GetCurrentYearSelection()
        {
            if (SelectedYear == DefaultYearOption)
                return (YearOptionKind.Default, null);

            if (SelectedYear == AllYearsOption)
                return (YearOptionKind.All, null);

            return (YearOptionKind.Year, SelectedYear);
        }

        private static (YearOptionKind? Kind, string? Year) GetPreferredYearSelection(UserSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.SelectedYearKind))
            {
                var kind = settings.SelectedYearKind.Trim().ToLowerInvariant();
                if (kind == "default")
                    return (YearOptionKind.Default, null);
                if (kind == "all")
                    return (YearOptionKind.All, null);
                if (kind == "year")
                    return (YearOptionKind.Year, settings.SelectedYearValue);
            }

            if (!string.IsNullOrWhiteSpace(settings.SelectedYearValue))
                return (YearOptionKind.Year, settings.SelectedYearValue);

            if (string.IsNullOrWhiteSpace(settings.SelectedYear))
                return (null, null);

            if (DefaultLabels.Contains(settings.SelectedYear))
                return (YearOptionKind.Default, null);

            if (AllLabels.Contains(settings.SelectedYear))
                return (YearOptionKind.All, null);

            return (YearOptionKind.Year, settings.SelectedYear);
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
