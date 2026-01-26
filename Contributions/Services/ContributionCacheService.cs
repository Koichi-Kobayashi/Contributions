using System.IO;
using System.Text.Json;
using Contributions.Models;

namespace Contributions.Services
{
    /// <summary>
    /// コントリビューションの取得結果をローカルにキャッシュするサービス。
    /// </summary>
    public class ContributionCacheService
    {
        private const int CacheVersion = 2;
        private const string CacheDirectoryName = "cache";

        private static string CacheDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Contributions",
                CacheDirectoryName);

        /// <summary>
        /// 既定表示用のコントリビューションをキャッシュから読み込む。
        /// </summary>
        public async Task<List<Contribution>?> LoadDefaultContributionsAsync(string username)
        {
            var path = GetDefaultPath(username);
            return await LoadAsync<List<Contribution>>(path);
        }

        /// <summary>
        /// 既定表示用のコントリビューションをキャッシュに保存する。
        /// </summary>
        public async Task SaveDefaultContributionsAsync(string username, List<Contribution> contributions)
        {
            var path = GetDefaultPath(username);
            await SaveAsync(path, contributions);
        }

        /// <summary>
        /// 指定年のデータをキャッシュから読み込む。
        /// </summary>
        public async Task<YearData?> LoadYearDataAsync(string username, string year)
        {
            var path = GetYearPath(username, year);
            return await LoadAsync<YearData>(path);
        }

        /// <summary>
        /// 指定年のデータをキャッシュに保存する。
        /// </summary>
        public async Task SaveYearDataAsync(string username, YearData data)
        {
            var path = GetYearPath(username, data.Year);
            await SaveAsync(path, data);
        }

        /// <summary>
        /// 既定表示のキャッシュファイルパスを返す。
        /// </summary>
        private static string GetDefaultPath(string username)
        {
            var userDirectory = GetUserDirectory(username);
            return Path.Combine(userDirectory, "default.json");
        }

        /// <summary>
        /// 指定年のキャッシュファイルパスを返す。
        /// </summary>
        private static string GetYearPath(string username, string year)
        {
            var userDirectory = GetUserDirectory(username);
            return Path.Combine(userDirectory, $"year-{year}.json");
        }

        /// <summary>
        /// ユーザー別キャッシュディレクトリを返す。
        /// </summary>
        private static string GetUserDirectory(string username)
        {
            var safe = Sanitize(username);
            return Path.Combine(CacheDirectory, safe);
        }

        /// <summary>
        /// 指定ユーザーのキャッシュを削除する。
        /// </summary>
        public Task ClearUserCacheAsync(string username)
        {
            var userDirectory = GetUserDirectory(username);
            try
            {
                if (Directory.Exists(userDirectory))
                    Directory.Delete(userDirectory, recursive: true);
            }
            catch
            {
                // ignore
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// ファイル名に使える安全な文字列へ変換する。
        /// </summary>
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

        /// <summary>
        /// キャッシュファイルを読み込む。
        /// </summary>
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

        /// <summary>
        /// キャッシュファイルへ保存する。
        /// </summary>
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
