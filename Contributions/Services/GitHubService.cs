using System.Globalization;
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
        /// <summary>
        /// 年ごとの取得範囲を表す。
        /// </summary>
        public record YearRange(string Year, string From, string To);

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
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("x-requested-with", "XMLHttpRequest");

            var response = await httpClient.GetStringAsync($"https://github.com/{username}?tab=contributions");
            var doc = new HtmlDocument();
            doc.LoadHtml(response);

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
            var days = await FetchContributionDaysAsync(username, from, to)
                ?? await FetchContributionDaysAsync(username, from, to, useProfilePage: true);
            var contributions = new List<Contribution>();
            var fromDate = DateTime.ParseExact(from, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            var toDate = DateTime.ParseExact(to, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

            if (days != null)
            {
                foreach (var day in days)
                {
                    var date = GetAttributeFromSelfOrDescendant(day, "data-date");
                    var levelStr = GetAttributeFromSelfOrDescendant(day, "data-level");
                    if (string.IsNullOrWhiteSpace(date))
                        continue;
                    if (!TryParseDate(date, out var parsedDate) || parsedDate < fromDate || parsedDate > toDate)
                        continue;
                    var intensity = int.TryParse(levelStr, out var l) ? l : 0;

                    contributions.Add(new Contribution
                    {
                        Date = date,
                        Count = 0,
                        Intensity = intensity
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
            var fromDate = DateTime.Today.AddDays(-370);

            var contributions = new List<Contribution>();
            var seenDates = new HashSet<string>();
            for (var year = fromDate.Year; year <= toDate.Year; year++)
            {
                var rangeFrom = year == fromDate.Year
                    ? fromDate
                    : new DateTime(year, 1, 1);
                var rangeTo = year == toDate.Year
                    ? toDate
                    : new DateTime(year, 12, 31);
                var from = rangeFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var to = rangeTo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var days = await FetchContributionDaysAsync(username, from, to)
                    ?? await FetchContributionDaysAsync(username, from, to, useProfilePage: true);

                if (days == null)
                    continue;

                foreach (var day in days)
                {
                    var date = GetAttributeFromSelfOrDescendant(day, "data-date");
                    var levelStr = GetAttributeFromSelfOrDescendant(day, "data-level");
                    if (string.IsNullOrWhiteSpace(date))
                        continue;
                    if (!TryParseDate(date, out var parsedDate) || parsedDate < fromDate || parsedDate > toDate)
                        continue;
                    if (!seenDates.Add(date))
                        continue;
                    var intensity = int.TryParse(levelStr, out var l) ? l : 0;

                    contributions.Add(new Contribution
                    {
                        Date = date,
                        Count = 0,
                        Intensity = intensity
                    });
                }
            }

            return contributions;
        }

        /// <summary>
        /// 指定期間のコントリビューション日ノードを取得する。
        /// </summary>
        private static async Task<HtmlNodeCollection?> FetchContributionDaysAsync(
            string username,
            string from,
            string to,
            bool useProfilePage = false)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("x-requested-with", "XMLHttpRequest");

            var fromParam = Uri.EscapeDataString(from);
            var toParam = Uri.EscapeDataString(to);
            var url = useProfilePage
                ? $"https://github.com/{username}?tab=contributions&from={fromParam}&to={toParam}"
                : $"https://github.com/users/{username}/contributions?from={fromParam}&to={toParam}";

            var response = await httpClient.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(response);

            var calendarTable = doc.DocumentNode.SelectSingleNode(
                "//table[contains(@class, 'ContributionCalendar-grid') and contains(@class, 'js-calendar-graph-table')]");
            return calendarTable?.SelectNodes(".//td[contains(@class, 'ContributionCalendar-day')]");
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
