using System.IO;
using System.Text.Json;
using Contributions.Models;

namespace Contributions.Services
{
    public class ContributionCacheService
    {
        private const int CacheVersion = 1;
        private const string CacheDirectoryName = "cache";

        private static string CacheDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Contributions",
                CacheDirectoryName);

        public async Task<List<Contribution>?> LoadDefaultContributionsAsync(string username)
        {
            var path = GetDefaultPath(username);
            return await LoadAsync<List<Contribution>>(path);
        }

        public async Task SaveDefaultContributionsAsync(string username, List<Contribution> contributions)
        {
            var path = GetDefaultPath(username);
            await SaveAsync(path, contributions);
        }

        public async Task<YearData?> LoadYearDataAsync(string username, string year)
        {
            var path = GetYearPath(username, year);
            return await LoadAsync<YearData>(path);
        }

        public async Task SaveYearDataAsync(string username, YearData data)
        {
            var path = GetYearPath(username, data.Year);
            await SaveAsync(path, data);
        }

        private static string GetDefaultPath(string username)
        {
            var userDirectory = GetUserDirectory(username);
            return Path.Combine(userDirectory, "default.json");
        }

        private static string GetYearPath(string username, string year)
        {
            var userDirectory = GetUserDirectory(username);
            return Path.Combine(userDirectory, $"year-{year}.json");
        }

        private static string GetUserDirectory(string username)
        {
            var safe = Sanitize(username);
            return Path.Combine(CacheDirectory, safe);
        }

        private static string Sanitize(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "unknown";

            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new char[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                var c = input[i];
                sanitized[i] = invalidChars.Contains(c) ? '_' : c;
            }

            return new string(sanitized).Trim();
        }

        private static async Task<T?> LoadAsync<T>(string path)
        {
            if (!File.Exists(path))
                return default;

            try
            {
                await using var stream = File.OpenRead(path);
                var envelope = await JsonSerializer.DeserializeAsync<CacheEnvelope<T>>(stream);
                if (envelope == null || envelope.Version != CacheVersion)
                    return default;
                return envelope.Data;
            }
            catch
            {
                return default;
            }
        }

        private static async Task SaveAsync<T>(string path, T data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var stream = File.Create(path);
            var options = new JsonSerializerOptions { WriteIndented = true };
            var envelope = new CacheEnvelope<T>(CacheVersion, DateTimeOffset.UtcNow, data);
            await JsonSerializer.SerializeAsync(stream, envelope, options);
        }

        private record CacheEnvelope<T>(int Version, DateTimeOffset SavedAt, T Data);
    }
}
