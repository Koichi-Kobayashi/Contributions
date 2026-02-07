using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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
    /// <summary>
    /// コントリビューション表示ページ。
    /// </summary>
    public partial class DataPage : INavigableView<DataViewModel>
    {
        public DataViewModel ViewModel { get; }

        private readonly ISnackbarService _snackbarService;
        private const float SingleChartHeight = 320f;
        private const float ChartSpacing = 40f;
        private bool _wasLoading;
        private string? _currentTooltipText;
        private readonly ToolTip _chartToolTip;

        /// <summary>
        /// 描画対象のチャート情報。
        /// </summary>
        private record ChartData(string Title, List<Contribution> Contributions, bool UseFullRange, string? TotalLabel);

        /// <summary>
        /// DataPageを生成する。
        /// </summary>
        public DataPage(DataViewModel viewModel, ISnackbarService snackbarService)
        {
            ViewModel = viewModel;
            _snackbarService = snackbarService;
            DataContext = this;

            InitializeComponent();
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            _wasLoading = ViewModel.IsLoading;
            _chartToolTip = new ToolTip
            {
                Placement = PlacementMode.MousePoint,
                PlacementTarget = ChartCanvas,
                StaysOpen = true
            };
            ChartCanvas.ToolTip = _chartToolTip;
        }

        /// <summary>
        /// ViewModelの変更を監視して表示を更新する。
        /// </summary>
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

        /// <summary>
        /// エラーメッセージをモーダルで表示する。
        /// </summary>
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

        /// <summary>
        /// チャートの描画処理。
        /// </summary>
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

        /// <summary>
        /// チャートの数に合わせてキャンバスサイズを調整する。
        /// </summary>
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

        /// <summary>
        /// 選択年に応じたチャート情報を取得する。
        /// </summary>
        private List<ChartData> GetChartItems()
        {
            var data = ViewModel.ContributionData;
            if (data == null)
                return [];

            var selected = ViewModel.SelectedYear?.Trim();
            if (string.IsNullOrWhiteSpace(selected) || selected == DataViewModel.DefaultYearOption)
            {
                if (data.DefaultContributions.Count > 0)
                    return
                    [
                        new ChartData(
                            "GitHub Contributions",
                            data.DefaultContributions,
                            false,
                            BuildTotalLabel(data.DefaultTotal, "in the last year"))
                    ];

                return
                [
                    new ChartData(
                        "GitHub Contributions",
                        data.Contributions,
                        false,
                        BuildTotalLabel(data.DefaultTotal, "in the last year"))
                ];
            }

            if (selected == DataViewModel.AllYearsOption)
            {
                return ViewModel.GetOrderedYears()
                    .Select(y => new ChartData(
                        $"GitHub Contributions {y.Year}",
                        y.Contributions,
                        true,
                        BuildTotalLabel(y.Total, $"in {y.Year}")))
                    .Where(c => c.Contributions.Count > 0)
                    .ToList();
            }

            var target = data.Years.FirstOrDefault(y => y.Year == selected);
            if (target != null)
                return
                [
                    new ChartData(
                        $"GitHub Contributions {target.Year}",
                        target.Contributions,
                        true,
                        BuildTotalLabel(target.Total, $"in {target.Year}"))
                ];

            return
            [
                new ChartData(
                    "GitHub Contributions",
                    data.Contributions,
                    false,
                    BuildTotalLabel(data.DefaultTotal, "in the last year"))
            ];
        }

        /// <summary>
        /// チャートを描画する。
        /// </summary>
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
            var minContributionDate = contributions
                .Select(c => DateTime.ParseExact(c.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture))
                .DefaultIfEmpty(DateTime.MinValue)
                .Min();

            var (startDate, rangeEndDate, weeks) = GetChartRange(chart);
            var chartHeight = 7 * dayHeight;

            var chartOffsetX = weekWidth;
            var startX = padding + 30 + chartOffsetX;
            var startY = offsetY + padding + 80;

            // 曜日ラベル（Mon/Wed/Fri）
            using var dayLabelPaint = new SKPaint
            {
                Color = SKColor.Parse(theme.SubText),
                IsAntialias = true
            };
            using var dayLabelFont = new SKFont(SKTypeface.Default, 12);
            DrawDayLabel(canvas, "Mon", 1);
            DrawDayLabel(canvas, "Wed", 3);
            DrawDayLabel(canvas, "Fri", 5);

            void DrawDayLabel(SKCanvas c, string text, int dayIndex)
            {
                var y = startY + dayIndex * dayHeight + cellSize / 2;
                c.DrawText(text, startX - 30, y + 4, SKTextAlign.Left, dayLabelFont, dayLabelPaint);
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

                    if (chart.UseFullRange && date < minContributionDate)
                        continue;

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

            // 合計ラベル（凡例と同じ縦位置で右寄せ）
            if (!string.IsNullOrWhiteSpace(chart.TotalLabel))
            {
                using var totalPaint = new SKPaint
                {
                    Color = SKColor.Parse(theme.Text),
                    IsAntialias = true
                };
                using var totalFont = new SKFont(SKTypeface.Default, 18);
                var totalBounds = new SKRect();
                totalFont.MeasureText(chart.TotalLabel, out totalBounds);
                var totalX = info.Width - padding - totalBounds.Width - 40;
                canvas.DrawText(
                    chart.TotalLabel,
                    totalX,
                    legendTextBaseline,
                    SKTextAlign.Left,
                    totalFont,
                    totalPaint);
            }

            static void DrawCell(SKCanvas canvas, float x, float y, float size, SKPaint paint)
            {
                var px = (float)Math.Round(x);
                var py = (float)Math.Round(y);
                canvas.DrawRect(px, py, size, size, paint);
            }
        }

        private void ChartCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            SetTooltipText(null, null);
        }

        private void ChartCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (ViewModel.ContributionData == null || !ViewModel.HasResult)
            {
                SetTooltipText(null, null);
                return;
            }

            var point = e.GetPosition(ChartCanvas);
            var tooltip = GetTooltipTextAt(point);
            SetTooltipText(tooltip, point);
        }

        private void SetTooltipText(string? text, Point? point)
        {
            _currentTooltipText = text;
            if (string.IsNullOrWhiteSpace(text))
            {
                _chartToolTip.IsOpen = false;
                return;
            }

            _chartToolTip.Content = text;
            _chartToolTip.HorizontalOffset = 12;
            _chartToolTip.VerticalOffset = 16;
            _chartToolTip.IsOpen = true;
        }

        private string? GetTooltipTextAt(Point point)
        {
            var charts = GetChartItems();
            if (charts.Count == 0)
                return null;

            var offsetY = 0f;
            foreach (var chart in charts)
            {
                if (point.Y >= offsetY && point.Y <= offsetY + SingleChartHeight)
                {
                    return GetTooltipTextAtChart(point, chart, offsetY);
                }

                offsetY += SingleChartHeight + ChartSpacing;
            }

            return null;
        }

        private static string? GetTooltipTextAtChart(Point point, ChartData chart, float offsetY)
        {
            const float padding = 40f;
            const float cellSize = 11f;
            const float cellSpacing = 3f;
            const float leftLabelOffset = 30f;
            const float chartTopOffset = 80f;
            const float chartOffsetX = cellSize + cellSpacing;

            var weekWidth = cellSize + cellSpacing;
            var dayHeight = cellSize + cellSpacing;
            var startX = padding + leftLabelOffset + chartOffsetX;
            var startY = offsetY + padding + chartTopOffset;

            var localX = (float)point.X - startX;
            var localY = (float)point.Y - startY;
            if (localX < 0 || localY < 0)
                return null;

            var week = (int)(localX / weekWidth);
            var day = (int)(localY / dayHeight);
            if (week < 0 || day < 0 || day > 6)
                return null;

            var cellX = localX - week * weekWidth;
            var cellY = localY - day * dayHeight;
            if (cellX > cellSize || cellY > cellSize)
                return null;

            var (startDate, rangeEndDate, weeks) = GetChartRange(chart);
            if (week >= weeks)
                return null;

            var date = startDate.AddDays(week * 7 + day);
            if (date < startDate || date > rangeEndDate)
                return null;

            var contributions = chart.Contributions;
            var minContributionDate = contributions
                .Select(c => DateTime.ParseExact(c.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture))
                .DefaultIfEmpty(DateTime.MinValue)
                .Min();
            if (chart.UseFullRange && date < minContributionDate)
                return null;

            var dateStr = date.ToString("yyyy-MM-dd");
            var contributionDict = contributions
                .GroupBy(c => c.Date)
                .ToDictionary(g => g.Key, g => g.Last());

            if (contributionDict.TryGetValue(dateStr, out var contribution)
                && !string.IsNullOrWhiteSpace(contribution.TooltipText))
            {
                return contribution.TooltipText;
            }

            return null;
        }

        /// <summary>
        /// チャートの表示範囲を算出する。
        /// </summary>
        private static (DateTime StartDate, DateTime EndDate, int Weeks) GetChartRange(ChartData chart)
        {
            var dates = chart.Contributions
                .Select(c => DateTime.ParseExact(c.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture))
                .OrderBy(d => d)
                .ToList();
            if (dates.Count == 0)
                return (DateTime.Today, DateTime.Today, 1);

            var lastDate = dates.Last();
            var localToday = DateTime.Today;
            if (lastDate.Date > localToday)
                lastDate = localToday;
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

        /// <summary>
        /// チャートの必要幅を計算する。
        /// </summary>
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

        /// <summary>
        /// クリップボードへコピーを実行する。
        /// </summary>
        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            CopyChartToClipboard(showSnackbar: true);
        }

        /// <summary>
        /// 現在のチャートを画像としてクリップボードにコピーする。
        /// </summary>
        private void CopyChartToClipboard(bool showSnackbar)
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.InvokeAsync(() => CopyChartToClipboard(showSnackbar));
                return;
            }

            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
                return;

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

                try
                {
                    Clipboard.SetImage(bitmapImage);
                }
                catch (NotSupportedException)
                {
                    return;
                }
                catch (System.Runtime.InteropServices.ExternalException)
                {
                    return;
                }
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

        /// <summary>
        /// X共有画面を開く。
        /// </summary>
        private void ShareXButton_Click(object sender, RoutedEventArgs e)
        {
            var username = GitHubService.CleanUsername(ViewModel.Url);
            var profileUrl = string.IsNullOrWhiteSpace(username) ? null : $"https://github.com/{username}";
            var shareText = ViewModel.ShareText?.TrimEnd() ?? string.Empty;

            var shareUrl = ViewModel.ShareUrlOption == DataViewModel.ShareUrlOptionNone
                ? null
                : profileUrl;

            if (!string.IsNullOrWhiteSpace(shareText) && !string.IsNullOrWhiteSpace(shareUrl))
            {
                shareText += "\n";
            }

            var shareHashtagValues = new[] { ViewModel.ShareHashtag1, ViewModel.ShareHashtag2, ViewModel.ShareHashtag3 };
            var (urls, hashtags) = SplitShareValues(shareHashtagValues);

            if (urls.Count > 0)
            {
                if (!string.IsNullOrEmpty(shareText))
                    shareText += "\n";
                shareText += string.Join("\n", urls);
            }

            if (!string.IsNullOrWhiteSpace(shareText) && !string.IsNullOrWhiteSpace(shareUrl))
            {
                shareText += "\n";
            }

            XShare.OpenTweetComposer(
                text: shareText,
                url: shareUrl,
                hashtags: hashtags);
        }

        /// <summary>
        /// Share hashtagの値をURLとハッシュタグに分離する。
        /// http:// または https:// で始まる値はURLとして扱い、#を付けずにテキストに含める（投稿時にリンクになる）。
        /// </summary>
        private static (List<string> Urls, string? Hashtags) SplitShareValues(string?[] values)
        {
            var urls = new List<string>();
            var tags = new List<string>();

            foreach (var value in values)
            {
                var trimmed = value?.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                if (IsUrl(trimmed))
                {
                    urls.Add(trimmed);
                }
                else
                {
                    var tag = NormalizeHashtag(trimmed);
                    if (!string.IsNullOrWhiteSpace(tag))
                        tags.Add(tag);
                }
            }

            var hashtags = tags.Count == 0 ? null : string.Join(",", tags);
            return (urls, hashtags);
        }

        private static bool IsUrl(string value)
        {
            return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        private static string? NormalizeHashtag(string? value)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                return null;

            trimmed = trimmed.TrimStart('#');
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        /// <summary>
        /// 強度値を0〜4の範囲に丸める。
        /// </summary>
        private static int ClampIntensity(int intensity)
        {
            if (intensity < 0) return 0;
            if (intensity > 4) return 4;
            return intensity;
        }

        private static string? BuildTotalLabel(int total, string suffix)
        {
            if (total < 0)
                return null;

            var formatted = total.ToString("N0", CultureInfo.InvariantCulture);
            return $"{formatted} contributions {suffix}";
        }

        /// <summary>
        /// 月番号から表示用ラベルを取得する。
        /// </summary>
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
