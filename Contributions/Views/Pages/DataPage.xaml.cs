using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using Contributions.Helpers;
using Contributions.Models;
using Contributions.Resources;
using Contributions.Services;
using Contributions.ViewModels.Pages;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace Contributions.Views.Pages
{
    public partial class DataPage : INavigableView<DataViewModel>
    {
        public DataViewModel ViewModel { get; }

        private readonly ISnackbarService _snackbarService;
        private const float SingleChartHeight = 320f;
        private const float ChartSpacing = 40f;
        private bool _wasLoading;

        private record ChartData(string Title, List<Contribution> Contributions, bool UseFullRange);

        public DataPage(DataViewModel viewModel, ISnackbarService snackbarService)
        {
            ViewModel = viewModel;
            _snackbarService = snackbarService;
            DataContext = this;

            InitializeComponent();
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            _wasLoading = ViewModel.IsLoading;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DataViewModel.ErrorMessage))
            {
                var message = ViewModel.ErrorMessage;
                if (!string.IsNullOrWhiteSpace(message))
                {
                    _ = ShowErrorMessageAsync(message);
                }
            }

            if (e.PropertyName == nameof(DataViewModel.ContributionData)
                || e.PropertyName == nameof(DataViewModel.SelectedYear))
            {
                UpdateChartCanvasSize();
                ChartCanvas.InvalidateVisual();

                if (e.PropertyName == nameof(DataViewModel.SelectedYear)
                    && ViewModel.AutoCopyToClipboard
                    && ViewModel.HasResult
                    && !ViewModel.IsLoading)
                {
                    CopyChartToClipboard(showSnackbar: false);
                }
            }
            else if (e.PropertyName == nameof(DataViewModel.ThemeMode)
                || e.PropertyName == nameof(DataViewModel.PaletteName))
            {
                ChartCanvas.InvalidateVisual();
            }
            else if (e.PropertyName == nameof(DataViewModel.IsLoading))
            {
                if (_wasLoading
                    && !ViewModel.IsLoading
                    && ViewModel.AutoCopyToClipboard
                    && ViewModel.HasResult)
                {
                    CopyChartToClipboard(showSnackbar: true);
                }

                _wasLoading = ViewModel.IsLoading;
            }
        }

        private async Task ShowErrorMessageAsync(string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.InvokeAsync(async () => await ShowErrorMessageAsync(message));
                return;
            }

            var messageBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = Translations.GetString("Error_Title"),
                Content = message,
                PrimaryButtonText = Translations.GetString("Error_Ok"),
                IsPrimaryButtonEnabled = true,
                IsSecondaryButtonEnabled = false,
                IsCloseButtonEnabled = false,
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            await messageBox.ShowDialogAsync();
        }

        private void ChartCanvas_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            if (ViewModel.ContributionData == null || ViewModel.ContributionData.Contributions.Count == 0)
                return;

            var theme = ViewModel.GetThemeColors();
            var paletteColors = ViewModel.GetPaletteColors();

            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColor.Parse(theme.Background));

            var info = e.Info;
            var charts = GetChartItems();
            if (charts.Count == 0)
                return;

            var offsetY = 0f;
            foreach (var chart in charts)
            {
                DrawChart(canvas, info, chart, theme, paletteColors, offsetY);
                offsetY += SingleChartHeight + ChartSpacing;
            }
        }

        private void UpdateChartCanvasSize()
        {
            if (ViewModel.ContributionData == null || ViewModel.ContributionData.Contributions.Count == 0)
                return;

            var charts = GetChartItems();
            var chartCount = Math.Max(1, charts.Count);
            ChartCanvas.Height = SingleChartHeight * chartCount + ChartSpacing * Math.Max(0, chartCount - 1);
            var maxWidth = charts.Count == 0
                ? 900f
                : charts.Max(ComputeChartWidth);
            ChartCanvas.Width = Math.Max(900f, maxWidth);
        }

        private List<ChartData> GetChartItems()
        {
            var data = ViewModel.ContributionData;
            if (data == null)
                return [];

            var selected = ViewModel.SelectedYear?.Trim();
            if (string.IsNullOrWhiteSpace(selected) || selected == DataViewModel.DefaultYearOption)
            {
                if (data.DefaultContributions.Count > 0)
                    return [new ChartData("GitHub Contributions", data.DefaultContributions, false)];

                return [new ChartData("GitHub Contributions", data.Contributions, false)];
            }

            if (selected == DataViewModel.AllYearsOption)
            {
                return ViewModel.GetOrderedYears()
                    .Select(y => new ChartData($"GitHub Contributions {y.Year}", y.Contributions, false))
                    .Where(c => c.Contributions.Count > 0)
                    .ToList();
            }

            var target = data.Years.FirstOrDefault(y => y.Year == selected);
            if (target != null)
                return [new ChartData($"GitHub Contributions {target.Year}", target.Contributions, false)];

            return [new ChartData("GitHub Contributions", data.Contributions, false)];
        }

        private void DrawChart(
            SKCanvas canvas,
            SKImageInfo info,
            ChartData chart,
            (string Background, string Text, string SubText) theme,
            string[] paletteColors,
            float offsetY)
        {
            if (chart.Contributions.Count == 0)
                return;

            var padding = 40f;
            var cellSize = 11f;
            var cellSpacing = 3f;
            var weekWidth = cellSize + cellSpacing;
            var dayHeight = cellSize + cellSpacing;

            // タイトル
            using var titlePaint = new SKPaint
            {
                Color = SKColor.Parse(theme.Text),
                IsAntialias = true
            };
            using var titleFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 24);
            var titleBounds = new SKRect();
            titleFont.MeasureText(chart.Title, out titleBounds);
            canvas.DrawText(
                chart.Title,
                (info.Width - titleBounds.Width) / 2,
                offsetY + padding + 30,
                SKTextAlign.Left,
                titleFont,
                titlePaint);

            var contributions = chart.Contributions;
            var contributionDict = contributions
                .GroupBy(c => c.Date)
                .ToDictionary(g => g.Key, g => g.Last());

            var (startDate, rangeEndDate, weeks) = GetChartRange(chart);
            var chartHeight = 7 * dayHeight;

            var startX = padding + 30;
            var startY = offsetY + padding + 80;

            // 曜日ラベル（Mon/Wed/Fri）
            using var dayLabelPaint = new SKPaint
            {
                Color = SKColor.Parse(theme.SubText),
                IsAntialias = true
            };
            using var dayLabelFont = new SKFont(SKTypeface.Default, 10);
            DrawDayLabel(canvas, "Mon", 1);
            DrawDayLabel(canvas, "Wed", 3);
            DrawDayLabel(canvas, "Fri", 5);

            void DrawDayLabel(SKCanvas c, string text, int dayIndex)
            {
                var y = startY + dayIndex * dayHeight + cellSize / 2;
                c.DrawText(text, startX - 30, y, SKTextAlign.Left, dayLabelFont, dayLabelPaint);
            }

            using var cellPaint = new SKPaint
            {
                IsAntialias = false,
                Style = SKPaintStyle.Fill
            };

            var currentDate = startDate;
            for (int week = 0; week < weeks; week++)
            {
                var weekStart = currentDate;

                // 月ラベル（その週内に1日があれば表示）
                DateTime? monthLabelDate = null;
                for (int i = 0; i < 7; i++)
                {
                    var d = weekStart.AddDays(i);
                    if (d.Day == 1)
                    {
                        monthLabelDate = d;
                        break;
                    }
                }

                if (monthLabelDate != null)
                {
                    using var monthPaint = new SKPaint
                    {
                        Color = SKColor.Parse(theme.SubText),
                        IsAntialias = true
                    };
                    using var monthFont = new SKFont(SKTypeface.Default, 12);
                    var month = GetMonthLabel(monthLabelDate.Value.Month);
                    canvas.DrawText(month, startX + week * weekWidth, startY - 12, SKTextAlign.Left, monthFont, monthPaint);
                }

                for (int day = 0; day < 7; day++)
                {
                    var date = weekStart.AddDays(day);
                    var dateStr = date.ToString("yyyy-MM-dd");

                    if (contributionDict.TryGetValue(dateStr, out var contribution))
                    {
                        var x = startX + week * weekWidth;
                        var y = startY + day * dayHeight;

                        cellPaint.Color = SKColor.Parse(paletteColors[ClampIntensity(contribution.Intensity)]);
                        DrawCell(canvas, x, y, cellSize, cellPaint);
                    }
                    else if (date >= startDate && date <= rangeEndDate)
                    {
                        var x = startX + week * weekWidth;
                        var y = startY + day * dayHeight;
                        cellPaint.Color = SKColor.Parse(paletteColors[0]);
                        DrawCell(canvas, x, y, cellSize, cellPaint);
                    }
                }

                currentDate = weekStart.AddDays(7);
            }

            // 凡例
            var legendY = startY + chartHeight + 50;
            using var legendLabelPaint = new SKPaint
            {
                Color = SKColor.Parse(theme.SubText),
                IsAntialias = true
            };
            using var legendFont = new SKFont(SKTypeface.Default, 12);
            var legendCenterY = legendY;
            var legendMetrics = legendFont.Metrics;
            var legendTextBaseline = legendCenterY - (legendMetrics.Ascent + legendMetrics.Descent) / 2;
            canvas.DrawText(
                "Less",
                startX,
                legendTextBaseline,
                SKTextAlign.Left,
                legendFont,
                legendLabelPaint);

            var legendX = startX + 50;
            for (int i = 0; i < paletteColors.Length; i++)
            {
                var x = legendX + i * (cellSize + cellSpacing + 5);
                cellPaint.Color = SKColor.Parse(paletteColors[i]);
                DrawCell(canvas, x, legendCenterY - cellSize / 2, cellSize, cellPaint);
            }
            canvas.DrawText(
                "More",
                legendX + paletteColors.Length * (cellSize + cellSpacing + 5) + 10,
                legendTextBaseline,
                SKTextAlign.Left,
                legendFont,
                legendLabelPaint);

            static void DrawCell(SKCanvas canvas, float x, float y, float size, SKPaint paint)
            {
                var px = (float)Math.Round(x);
                var py = (float)Math.Round(y);
                canvas.DrawRect(px, py, size, size, paint);
            }
        }

        private static (DateTime StartDate, DateTime EndDate, int Weeks) GetChartRange(ChartData chart)
        {
            var dates = chart.Contributions.Select(c => DateTime.Parse(c.Date)).OrderBy(d => d).ToList();
            if (dates.Count == 0)
                return (DateTime.Today, DateTime.Today, 1);

            var lastDate = dates.Last();
            var localToday = DateTime.Today;
            var dayDelta = (localToday - lastDate.Date).Days;
            if (dayDelta == 1)
            {
                lastDate = localToday;
            }

            var endWeekStart = lastDate;
            while (endWeekStart.DayOfWeek != DayOfWeek.Sunday)
                endWeekStart = endWeekStart.AddDays(-1);

            if (!chart.UseFullRange)
            {
                const int weeks = 53;
                var startDate = endWeekStart.AddDays(-7 * (weeks - 1));
                return (startDate, lastDate, weeks);
            }

            var firstDate = dates.First();
            var startWeekStart = firstDate;
            while (startWeekStart.DayOfWeek != DayOfWeek.Sunday)
                startWeekStart = startWeekStart.AddDays(-1);

            var totalWeeks = (int)((endWeekStart - startWeekStart).TotalDays / 7) + 1;
            if (totalWeeks < 1)
                totalWeeks = 1;

            return (startWeekStart, lastDate, totalWeeks);
        }

        private static float ComputeChartWidth(ChartData chart)
        {
            const float padding = 40f;
            const float cellSize = 11f;
            const float cellSpacing = 3f;
            const float leftLabelOffset = 30f;

            var (_, _, weeks) = GetChartRange(chart);
            var weekWidth = cellSize + cellSpacing;
            var startX = padding + leftLabelOffset;
            return startX + weeks * weekWidth + padding;
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            CopyChartToClipboard(showSnackbar: true);
        }

        private void CopyChartToClipboard(bool showSnackbar)
        {
            if (ViewModel.ContributionData == null)
                return;

            try
            {
                var charts = GetChartItems();
                if (charts.Count == 0)
                    return;

                var chartCount = Math.Max(1, charts.Count);
                var calculatedHeight = SingleChartHeight * chartCount + ChartSpacing * Math.Max(0, chartCount - 1);
                var maxWidth = charts.Max(ComputeChartWidth);
                var calculatedWidth = Math.Max(900f, maxWidth);

                var width = (int)Math.Round(calculatedWidth);
                var height = (int)Math.Round(calculatedHeight);

                var info = new SKImageInfo(width, height);
                using var surface = SKSurface.Create(info);
                var args = new SKPaintSurfaceEventArgs(surface, info);
                ChartCanvas_PaintSurface(null, args);

                using var image = surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = new MemoryStream(data.ToArray());

                var bitmapImage = new System.Windows.Media.Imaging.BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = stream;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                Clipboard.SetImage(bitmapImage);    // ここで例外が出るが無視してOK
                ViewModel.CanShareToX = true;

                if (showSnackbar)
                {
                    _snackbarService.Show(
                        Translations.GetString("Snackbar_CopiedTitle"),
                        Translations.GetString("Snackbar_CopiedDetail"),
                        ControlAppearance.Success,
                        new SymbolIcon(SymbolRegular.Checkmark24),
                        TimeSpan.FromSeconds(4));
                }
            }
            catch
            {
                // ignore
            }
        }

        private void ShareXButton_Click(object sender, RoutedEventArgs e)
        {
            var username = GitHubService.CleanUsername(ViewModel.Url);
            var profileUrl = string.IsNullOrWhiteSpace(username) ? null : $"https://github.com/{username}";

            XShare.OpenTweetComposer(
                text: "My GitHub contributions this year 🚀\n",
                url: profileUrl,
                hashtags: "GitHub");
        }

        private static int ClampIntensity(int intensity)
        {
            if (intensity < 0) return 0;
            if (intensity > 4) return 4;
            return intensity;
        }

        private static string GetMonthLabel(int month)
        {
            var labels = new[]
            {
                "Jan", "Feb", "Mar", "Apr", "May", "Jun",
                "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
            };

            if (month < 1 || month > 12)
                return string.Empty;

            return labels[month - 1];
        }
    }
}
