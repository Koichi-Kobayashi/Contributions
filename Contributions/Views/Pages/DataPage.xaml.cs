using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using Contributions.Helpers;
using Contributions.Services;
using Contributions.Models;
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

        public DataPage(DataViewModel viewModel, ISnackbarService snackbarService)
        {
            ViewModel = viewModel;
            _snackbarService = snackbarService;
            DataContext = this;

            InitializeComponent();
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DataViewModel.ContributionData)
                || e.PropertyName == nameof(DataViewModel.ThemeMode)
                || e.PropertyName == nameof(DataViewModel.PaletteName))
            {
                ChartCanvas.InvalidateVisual();
            }
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
            var padding = 40f;
            var cellSize = 11f;
            var cellSpacing = 3f;
            var weekWidth = cellSize + cellSpacing;
            var dayHeight = cellSize + cellSpacing;

            // タイトル
            using var titlePaint = new SKPaint
            {
                Color = SKColor.Parse(theme.Text),
                TextSize = 24,
                IsAntialias = true,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
            };
            var titleText = "GitHub Contributions";
            var titleBounds = new SKRect();
            titlePaint.MeasureText(titleText, ref titleBounds);
            canvas.DrawText(titleText, (info.Width - titleBounds.Width) / 2, padding + 30, titlePaint);

            var contributions = ViewModel.ContributionData.Contributions;
            var contributionDict = contributions
                .GroupBy(c => c.Date)
                .ToDictionary(g => g.Key, g => g.Last());

            var dates = contributions.Select(c => DateTime.Parse(c.Date)).OrderBy(d => d).ToList();
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

            const int weeks = 53;
            var startDate = endWeekStart.AddDays(-7 * (weeks - 1));
            var rangeEndDate = lastDate;
            var chartHeight = 7 * dayHeight;

            var startX = padding + 30;
            var startY = padding + 80;

            // 曜日ラベル（Mon/Wed/Fri）
            using var dayLabelPaint = new SKPaint
            {
                Color = SKColor.Parse(theme.SubText),
                TextSize = 10,
                IsAntialias = true
            };
            DrawDayLabel(canvas, "Mon", 1);
            DrawDayLabel(canvas, "Wed", 3);
            DrawDayLabel(canvas, "Fri", 5);

            void DrawDayLabel(SKCanvas c, string text, int dayIndex)
            {
                var y = startY + dayIndex * dayHeight + cellSize / 2;
                c.DrawText(text, startX - 30, y, dayLabelPaint);
            }

            using var cellPaint = new SKPaint
            {
                IsAntialias = false,
                FilterQuality = SKFilterQuality.None,
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
                        TextSize = 12,
                        IsAntialias = true
                    };
                    var month = GetMonthLabel(monthLabelDate.Value.Month);
                    canvas.DrawText(month, startX + week * weekWidth, startY - 12, monthPaint);
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
                TextSize = 12,
                IsAntialias = true
            };
            var legendCenterY = legendY;
            var legendMetrics = legendLabelPaint.FontMetrics;
            var legendTextBaseline = legendCenterY - (legendMetrics.Ascent + legendMetrics.Descent) / 2;
            canvas.DrawText("Less", startX, legendTextBaseline, legendLabelPaint);

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
                legendLabelPaint);

            static void DrawCell(SKCanvas canvas, float x, float y, float size, SKPaint paint)
            {
                var px = (float)Math.Round(x);
                var py = (float)Math.Round(y);
                canvas.DrawRect(px, py, size, size, paint);
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.ContributionData == null)
                return;

            try
            {
                var width = (int)Math.Round(ChartCanvas.ActualWidth);
                var height = (int)Math.Round(ChartCanvas.ActualHeight);
                if (width <= 0 || height <= 0)
                {
                    width = 900;
                    height = 600;
                }

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

                _snackbarService.Show(
                    "Copied to clipboard.",
                    "You can post it to X using the Post to X button.",
                    ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.Checkmark24),
                    TimeSpan.FromSeconds(2));
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
                text: "My GitHub contributions this year 🚀\n(press Ctrl+V to paste the image)",
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
