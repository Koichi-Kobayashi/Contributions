using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Contributions.Models;
using HtmlAgilityPack;

namespace Contributions.Services
{
    /// <summary>
    /// GitHubのプロフィールページからコントリビューション情報を取得するサービス。
    /// </summary>
    public class GitHubService
    {
        private readonly WebViewHtmlService _webViewHtmlService;
        private readonly Dictionary<string, (DateTimeOffset CachedAt, string Html)> _profileHtmlCache = new();
        private static readonly TimeSpan ProfileHtmlCacheTtl = TimeSpan.FromMinutes(2);

        /// <summary>
        /// 年ごとの取得範囲を表す。
        /// </summary>
        public record YearRange(string Year, string From, string To);

        /// <summary>
        /// GitHubServiceを生成する。
        /// </summary>
        public GitHubService(WebViewHtmlService webViewHtmlService)
        {
            _webViewHtmlService = webViewHtmlService;
        }

        /// <summary>
        /// 入力文字列からGitHubユーザー名を抽出する。
        /// </summary>
        public static string CleanUsername(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var urlMatch = Regex.Match(input, @"github\.com/([^\/\?]+)");
            if (urlMatch.Success)
                return urlMatch.Groups[1].Value;

            return Regex.Replace(input, @"^(http|https)://(?!www\.)github\.com/", "").Trim();
        }

        /// <summary>
        /// 全年分のデータを取得して集約する。
        /// </summary>
        public async Task<ContributionData> FetchDataForAllYearsAsync(string username)
        {
            var defaultContributions = await FetchDefaultContributionsAsync(username);
            var years = await FetchYearsAsync(username);
            var yearDataList = new List<YearData>();
            var allContributions = new List<Contribution>();

            foreach (var year in years)
            {
                var yearData = await FetchDataForYearAsync(username, year.From, year.To, year.Year);
                yearDataList.Add(new YearData
                {
                    Year = yearData.Year,
                    Total = yearData.Total,
                    Range = yearData.Range,
                    Contributions = yearData.Contributions
                });
                allContributions.AddRange(yearData.Contributions);
            }

            allContributions = allContributions.OrderByDescending(c => c.Date).ToList();

            return new ContributionData
            {
                Years = yearDataList,
                Contributions = allContributions,
                DefaultContributions = defaultContributions
            };
        }

        /// <summary>
        /// プロフィールページから利用可能な年一覧を取得する。
        /// </summary>
        public async Task<List<YearRange>> FetchYearsAsync(string username)
        {
            var doc = await FetchProfileDocumentAsync(username);

            var yearLinks = doc.DocumentNode.SelectNodes(
                "//ul[contains(@class, 'filter-list') and contains(@class, 'small')]//a[starts-with(@id, 'year-link-')]")
                ?? doc.DocumentNode.SelectNodes("//a[contains(@class, 'js-year-link') and contains(@class, 'filter-item')]");
            var years = new List<YearRange>();

            if (yearLinks != null)
            {
                foreach (var link in yearLinks)
                {
                    var href = link.GetAttributeValue("href", "");
                    var text = NormalizeYearText(link.InnerText);
                    if (string.IsNullOrWhiteSpace(text))
                        continue;

                    var from = ExtractQueryValue(href, "from") ?? $"{text}-01-01";
                    var to = ExtractQueryValue(href, "to") ?? $"{text}-12-31";
                    years.Add(new YearRange(text, from, to));
                }
            }

            return years;
        }

        /// <summary>
        /// 指定年の期間から日別コントリビューションを取得する。
        /// </summary>
        private async Task<(string Year, int Total, DateRange? Range, List<Contribution> Contributions)> FetchDataForYearAsync(
            string username,
            string from,
            string to,
            string year)
        {
            var (days, doc) = await FetchContributionDaysWithDocumentAsync(username, from, to);
            var contributions = new List<Contribution>();
            var fromDate = DateTime.ParseExact(from, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            var toDate = DateTime.ParseExact(to, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            var tooltipLookup = BuildTooltipLookup(doc);

            if (days != null)
            {
                foreach (var day in days)
                {
                    var date = GetAttributeFromSelfOrDescendant(day, "data-date");
                    var levelStr = GetAttributeFromSelfOrDescendant(day, "data-level");
                    var tooltipText = GetTooltipTextForDay(day, tooltipLookup);
                    if (string.IsNullOrWhiteSpace(date))
                        continue;
                    if (!TryParseDate(date, out var parsedDate) || parsedDate < fromDate || parsedDate > toDate)
                        continue;
                    var intensity = int.TryParse(levelStr, out var l) ? l : 0;

                    contributions.Add(new Contribution
                    {
                        Date = date,
                        Count = 0,
                        Intensity = intensity,
                        TooltipText = tooltipText
                    });
                }
            }

            string? startDate = null;
            string? endDate = null;
            if (days != null && days.Count > 0)
            {
                startDate = days[0].GetAttributeValue("data-date", "");
                endDate = days[days.Count - 1].GetAttributeValue("data-date", "");
            }

            var range = startDate != null && endDate != null
                ? new DateRange { Start = startDate, End = endDate }
                : null;

            return (year, contributions.Count, range, contributions);
        }

        /// <summary>
        /// 既定表示用（最新年相当）のコントリビューションを取得する。
        /// </summary>
        public async Task<List<Contribution>> FetchDefaultContributionsAsync(string username)
        {
            var toDate = DateTime.Today;
            var fromDate = DateTime.Today.AddDays(-365);

            var contributions = new List<Contribution>();
            var seenDates = new HashSet<string>();

            if (fromDate.Year == toDate.Year)
            {
                await AppendDefaultRangeAsync(username, fromDate, toDate, contributions, seenDates);
                return contributions;
            }

            var endOfFromYear = new DateTime(fromDate.Year, 12, 31);
            var startOfToYear = new DateTime(toDate.Year, 1, 1);

            await AppendDefaultRangeAsync(username, fromDate, endOfFromYear, contributions, seenDates);
            await AppendDefaultRangeAsync(username, startOfToYear, toDate, contributions, seenDates);

            return contributions;
        }

        private async Task AppendDefaultRangeAsync(
            string username,
            DateTime rangeFrom,
            DateTime rangeTo,
            List<Contribution> contributions,
            HashSet<string> seenDates)
        {
            var from = rangeFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var to = rangeTo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var (days, doc) = await FetchContributionDaysWithDocumentAsync(username, from, to);
            if (days == null)
                return;

            var tooltipLookup = BuildTooltipLookup(doc);
            foreach (var day in days)
            {
                var date = GetAttributeFromSelfOrDescendant(day, "data-date");
                var levelStr = GetAttributeFromSelfOrDescendant(day, "data-level");
                var tooltipText = GetTooltipTextForDay(day, tooltipLookup);
                if (string.IsNullOrWhiteSpace(date))
                    continue;
                if (!TryParseDate(date, out var parsedDate) || parsedDate < rangeFrom || parsedDate > rangeTo)
                    continue;
                if (!seenDates.Add(date))
                    continue;
                var intensity = int.TryParse(levelStr, out var l) ? l : 0;

                contributions.Add(new Contribution
                {
                    Date = date,
                    Count = 0,
                    Intensity = intensity,
                    TooltipText = tooltipText
                });
            }
        }

        /// <summary>
        /// 指定期間のコントリビューション日ノードを取得する。
        /// </summary>
        private async Task<(HtmlNodeCollection? Days, HtmlDocument Document)> FetchContributionDaysWithDocumentAsync(
            string username,
            string from,
            string to)
        {
            var doc = await FetchProfileDocumentAsync(username, from, to);

            var calendarTable = doc.DocumentNode.SelectSingleNode(
                "//table[contains(@class, 'ContributionCalendar-grid') and contains(@class, 'js-calendar-graph-table')]");
            var days = calendarTable?.SelectNodes(".//td[contains(@class, 'ContributionCalendar-day')]");
            return (days, doc);
        }

        /// <summary>
        /// プロフィールページのHTMLを取得する。
        /// </summary>
        private async Task<HtmlDocument> FetchProfileDocumentAsync(
            string username,
            string? from = null,
            string? to = null)
        {
            var url = BuildProfileUrl(username, from, to);
            if (TryGetCachedProfileHtml(url, out var cachedHtml))
                return LoadHtmlDocument(cachedHtml);

            var webViewHtml = await _webViewHtmlService.LoadHtmlAsync(url);
            if (!string.IsNullOrWhiteSpace(webViewHtml))
            {
                var webDoc = new HtmlDocument();
                webDoc.LoadHtml(webViewHtml);
                if (ContainsContributionCalendar(webDoc) || IsFullHtmlDocument(webViewHtml))
                {
                    CacheProfileHtml(url, webViewHtml);
                    return webDoc;
                }
            }

            var response = await FetchHtmlWithHttpClientAsync(url);
            var fallback = new HtmlDocument();
            fallback.LoadHtml(response ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(response))
                CacheProfileHtml(url, response);
            return fallback;
        }

        private static string BuildProfileUrl(string username, string? from, string? to)
        {
            var url = $"https://github.com/{username}?tab=contributions";
            if (!string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to))
            {
                var fromParam = Uri.EscapeDataString(from);
                var toParam = Uri.EscapeDataString(to);
                url += $"&from={fromParam}&to={toParam}";
            }

            return url;
        }

        private static async Task<string?> FetchHtmlWithHttpClientAsync(string url)
        {
            using var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.All
            };
            using var httpClient = new HttpClient(handler);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Contributions-App");
            httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            return await httpClient.GetStringAsync(url);
        }

        /// <summary>
        /// 指定属性を自身または子要素から取得する。
        /// </summary>
        private static string GetAttributeFromSelfOrDescendant(HtmlNode node, string attributeName)
        {
            var direct = node.GetAttributeValue(attributeName, "");
            if (!string.IsNullOrWhiteSpace(direct))
                return direct;

            var child = node.SelectSingleNode($".//*[@{attributeName}]");
            return child?.GetAttributeValue(attributeName, "") ?? string.Empty;
        }

        /// <summary>
        /// 年表記を正規化して4桁の年を抽出する。
        /// </summary>
        private static string NormalizeYearText(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var match = Regex.Match(input, @"\d{4}");
            if (match.Success)
                return match.Value;

            return input.Trim();
        }

        private static Dictionary<string, string> BuildTooltipLookup(HtmlDocument doc)
        {
            var lookup = new Dictionary<string, string>();
            var tooltips = doc.DocumentNode.SelectNodes("//tool-tip");
            if (tooltips == null)
                return lookup;

            foreach (var tip in tooltips)
            {
                var text = NormalizeTooltipText(HtmlEntity.DeEntitize(tip.InnerText));
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                var forId = tip.GetAttributeValue("for", "");
                if (!string.IsNullOrWhiteSpace(forId))
                    lookup[forId] = text;

                var id = tip.GetAttributeValue("id", "");
                if (!string.IsNullOrWhiteSpace(id))
                    lookup[id] = text;

                var dateMatch = Regex.Match(forId, @"\d{4}-\d{2}-\d{2}");
                if (dateMatch.Success)
                    lookup[dateMatch.Value] = text;
            }

            return lookup;
        }

        private static string GetTooltipTextForDay(HtmlNode day, IReadOnlyDictionary<string, string> lookup)
        {
            var ariaLabel = GetAttributeFromSelfOrDescendant(day, "aria-label");
            if (!string.IsNullOrWhiteSpace(ariaLabel))
                return NormalizeTooltipText(ariaLabel);

            var labelledBy = GetAttributeFromSelfOrDescendant(day, "aria-labelledby");
            if (!string.IsNullOrWhiteSpace(labelledBy))
            {
                foreach (var id in labelledBy.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (lookup.TryGetValue(id, out var text))
                        return text;
                }
            }

            var nodeId = GetAttributeFromSelfOrDescendant(day, "id");
            if (!string.IsNullOrWhiteSpace(nodeId) && lookup.TryGetValue(nodeId, out var byId))
                return byId;

            var date = GetAttributeFromSelfOrDescendant(day, "data-date");
            if (!string.IsNullOrWhiteSpace(date) && lookup.TryGetValue(date, out var byDate))
                return byDate;

            return string.Empty;
        }

        private static string NormalizeTooltipText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return Regex.Replace(text, @"\s+", " ").Trim();
        }

        private static bool IsFullHtmlDocument(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return false;
            return Regex.IsMatch(html, "<html\\b", RegexOptions.IgnoreCase);
        }

        private static bool ContainsContributionCalendar(HtmlDocument doc)
        {
            return doc.DocumentNode.SelectSingleNode(
                "//table[contains(@class, 'ContributionCalendar-grid') and contains(@class, 'js-calendar-graph-table')]") != null;
        }

        private bool TryGetCachedProfileHtml(string url, out string html)
        {
            if (_profileHtmlCache.TryGetValue(url, out var entry))
            {
                if (DateTimeOffset.UtcNow - entry.CachedAt <= ProfileHtmlCacheTtl)
                {
                    html = entry.Html;
                    return true;
                }

                _profileHtmlCache.Remove(url);
            }

            html = string.Empty;
            return false;
        }

        private void CacheProfileHtml(string url, string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return;

            _profileHtmlCache[url] = (DateTimeOffset.UtcNow, html);
        }

        private static HtmlDocument LoadHtmlDocument(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            return doc;
        }

        /// <summary>
        /// プロフィールHTMLのメモリキャッシュをクリアする。
        /// </summary>
        public void ClearProfileHtmlCache()
        {
            _profileHtmlCache.Clear();
        }

        private static bool TryParseDate(string date, out DateTime parsed)
        {
            return DateTime.TryParseExact(
                date,
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out parsed);
        }

        /// <summary>
        /// クエリ文字列から指定キーの値を抽出する。
        /// </summary>
        private static string? ExtractQueryValue(string url, string key)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            var queryStart = url.IndexOf('?', StringComparison.Ordinal);
            if (queryStart < 0 || queryStart == url.Length - 1)
                return null;

            var query = url[(queryStart + 1)..];
            var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var kvp = part.Split('=', 2);
                if (kvp.Length != 2)
                    continue;
                if (string.Equals(kvp[0], key, StringComparison.OrdinalIgnoreCase))
                    return Uri.UnescapeDataString(kvp[1]);
            }

            return null;
        }

        /// <summary>
        /// 指定年のデータを取得してYearDataに変換する。
        /// </summary>
        public async Task<YearData> FetchYearDataAsync(string username, YearRange range)
        {
            var yearData = await FetchDataForYearAsync(username, range.From, range.To, range.Year);
            return new YearData
            {
                Year = yearData.Year,
                Total = yearData.Total,
                Range = yearData.Range,
                Contributions = yearData.Contributions
            };
        }

    }
}
