using System.Net.Http;
using System.Text.RegularExpressions;
using Contributions.Models;
using HtmlAgilityPack;

namespace Contributions.Services
{
    public class GitHubService
    {
        public static string CleanUsername(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var urlMatch = Regex.Match(input, @"github\.com/([^\/\?]+)");
            if (urlMatch.Success)
                return urlMatch.Groups[1].Value;

            return Regex.Replace(input, @"^(http|https)://(?!www\.)github\.com/", "").Trim();
        }

        public async Task<ContributionData> FetchDataForAllYearsAsync(string username)
        {
            var years = await FetchYearsAsync(username);
            var yearDataList = new List<YearData>();
            var allContributions = new List<Contribution>();

            foreach (var year in years)
            {
                var yearData = await FetchDataForYearAsync(year.Href, year.Text);
                yearDataList.Add(new YearData
                {
                    Year = yearData.Year,
                    Total = yearData.Total,
                    Range = yearData.Range
                });
                allContributions.AddRange(yearData.Contributions);
            }

            allContributions = allContributions.OrderByDescending(c => c.Date).ToList();

            return new ContributionData
            {
                Years = yearDataList,
                Contributions = allContributions
            };
        }

        private async Task<List<(string Href, string Text)>> FetchYearsAsync(string username)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("x-requested-with", "XMLHttpRequest");

            var response = await httpClient.GetStringAsync($"https://github.com/{username}?tab=contributions");
            var doc = new HtmlDocument();
            doc.LoadHtml(response);

            var yearLinks = doc.DocumentNode.SelectNodes("//a[contains(@class, 'js-year-link') and contains(@class, 'filter-item')]");
            var years = new List<(string Href, string Text)>();

            if (yearLinks != null)
            {
                foreach (var link in yearLinks)
                {
                    var href = link.GetAttributeValue("href", "");
                    var uri = new Uri($"https://github.com{href}");
                    var builder = new UriBuilder(uri)
                    {
                        Query = "tab=contributions"
                    };
                    var formattedHref = builder.Path + builder.Query;

                    years.Add((formattedHref, link.InnerText.Trim()));
                }
            }

            return years;
        }

        private async Task<(string Year, int Total, DateRange? Range, List<Contribution> Contributions)> FetchDataForYearAsync(string url, string year)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("x-requested-with", "XMLHttpRequest");

            var response = await httpClient.GetStringAsync($"https://github.com{url}");
            var doc = new HtmlDocument();
            doc.LoadHtml(response);

            var calendarTable = doc.DocumentNode.SelectSingleNode(
                "//table[contains(@class, 'ContributionCalendar-grid') and contains(@class, 'js-calendar-graph-table')]");
            var days = calendarTable?.SelectNodes(".//td[contains(@class, 'ContributionCalendar-day')]");
            var contributions = new List<Contribution>();

            if (days != null)
            {
                foreach (var day in days)
                {
                    var date = GetAttributeFromSelfOrDescendant(day, "data-date");
                    var levelStr = GetAttributeFromSelfOrDescendant(day, "data-level");
                    if (string.IsNullOrWhiteSpace(date))
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

        private static string GetAttributeFromSelfOrDescendant(HtmlNode node, string attributeName)
        {
            var direct = node.GetAttributeValue(attributeName, "");
            if (!string.IsNullOrWhiteSpace(direct))
                return direct;

            var child = node.SelectSingleNode($".//*[@{attributeName}]");
            return child?.GetAttributeValue(attributeName, "") ?? string.Empty;
        }

    }
}
