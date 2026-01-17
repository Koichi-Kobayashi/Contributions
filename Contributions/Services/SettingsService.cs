using System.IO;
using System.Text.Json;
using Contributions.Models;

namespace Contributions.Services
{
    public class SettingsService
    {
        private const string SettingsFileName = "settings.json";

        private static string SettingsDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Contributions");

        private static string SettingsPath => Path.Combine(SettingsDirectory, SettingsFileName);

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

        public async Task SaveAsync(UserSettings settings)
        {
            Directory.CreateDirectory(SettingsDirectory);

            await using var stream = File.Create(SettingsPath);
            var options = new JsonSerializerOptions { WriteIndented = true };
            await JsonSerializer.SerializeAsync(stream, settings, options);
        }
    }
}
