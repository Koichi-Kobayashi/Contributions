using System.Text.Json;
using System.Threading;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Contributions.Services
{
    /// <summary>
    /// 見えないWebView2でHTMLを取得する。
    /// </summary>
    public class WebViewHtmlService
    {
        private const int DefaultTimeoutMs = 20000;
        private const int PollIntervalMs = 400;

        public async Task<string> LoadHtmlAsync(string url, int timeoutMs = DefaultTimeoutMs)
        {
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;

            if (Application.Current == null)
                return string.Empty;

            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource(timeoutMs);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var window = new Window
                {
                    Width = 1,
                    Height = 1,
                    WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Opacity = 0,
                    AllowsTransparency = true,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -20000,
                    Top = -20000,
                    WindowState = WindowState.Minimized
                };

                var webView = new WebView2();

                void Cleanup(string? html)
                {
                    webView.NavigationCompleted -= OnNavigationCompleted;
                    webView.CoreWebView2InitializationCompleted -= OnCoreWebView2InitializationCompleted;
                    window.Content = null;
                    window.Close();
                    tcs.TrySetResult(html ?? string.Empty);
                }

                async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
                {
                    if (!e.IsSuccess)
                    {
                        Cleanup(string.Empty);
                        return;
                    }

                    try
                    {
                        var html = await WaitForHtmlAsync(webView, cts.Token);
                        Cleanup(html);
                    }
                    catch
                    {
                        Cleanup(string.Empty);
                    }
                }

                void OnCoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
                {
                    if (!e.IsSuccess)
                    {
                        Cleanup(string.Empty);
                        return;
                    }

                    try
                    {
                        webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                        webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
                        webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                        webView.CoreWebView2.Settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
                    }
                    catch
                    {
                        // ignore
                    }
                }

                async Task InitializeAsync()
                {
                    try
                    {
                        webView.CoreWebView2InitializationCompleted += OnCoreWebView2InitializationCompleted;
                        await webView.EnsureCoreWebView2Async();
                        webView.Source = new Uri(url);
                    }
                    catch
                    {
                        Cleanup(string.Empty);
                    }
                }

                webView.NavigationCompleted += OnNavigationCompleted;
                window.Content = webView;
                window.Show();

                _ = InitializeAsync();
            });

            await using (cts.Token.Register(() => tcs.TrySetResult(string.Empty)))
            {
                return await tcs.Task.ConfigureAwait(false);
            }
        }

        private static async Task<string> WaitForHtmlAsync(WebView2 webView, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (await IsCalendarReadyAsync(webView))
                {
                    var htmlResult = await webView.ExecuteScriptAsync("document.documentElement.outerHTML");
                    return JsonSerializer.Deserialize<string>(htmlResult) ?? string.Empty;
                }

                await Task.Delay(PollIntervalMs, token);
            }

            return string.Empty;
        }

        private static async Task<bool> IsCalendarReadyAsync(WebView2 webView)
        {
            var script = """
                (function() {
                    if (document.readyState !== 'complete') return false;
                    return !!document.querySelector('table.ContributionCalendar-grid.js-calendar-graph-table');
                })();
                """;
            try
            {
                var result = await webView.ExecuteScriptAsync(script);
                return JsonSerializer.Deserialize<bool>(result);
            }
            catch
            {
                return false;
            }
        }
    }
}
