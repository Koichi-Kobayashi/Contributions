using System.IO;
using System.Text.Json;
using Contributions.Models;

namespace Contributions.Services
{
    /// <summary>
    /// アプリ設定の読み書きを行うサービス。
    /// </summary>
    public class SettingsService
    {
        private const string SettingsFileName = "settings.json";

        private static string SettingsDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Contributions");

        private static string SettingsPath => Path.Combine(SettingsDirectory, SettingsFileName);

        /// <summary>
        /// 設定ファイルから設定を読み込む。
        /// </summary>
        public async Task<UserSettings> LoadAsync()
        {
            if (!File.Exists(SettingsPath))
                return new UserSettings();

            try
            {
                await using var stream = File.OpenRead(SettingsPath);
                var settings = await JsonSerializer.DeserializeAsync<UserSettings>(stream);
                return settings ?? new UserSettings();
            }
            catch
            {
                return new UserSettings();
            }
        }

        /// <summary>
        /// 設定ファイルへ設定を保存する。
        /// </summary>
        public async Task SaveAsync(UserSettings settings)
        {
            Directory.CreateDirectory(SettingsDirectory);

            await using var stream = File.Create(SettingsPath);
            var options = new JsonSerializerOptions { WriteIndented = true };
            await JsonSerializer.SerializeAsync(stream, settings, options);
        }
    }
}
