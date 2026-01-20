namespace Contributions.Models
{
    public class UserSettings
    {
        public string Url { get; set; } = string.Empty;

        public string ThemeMode { get; set; } = "Dark";

        public string PaletteName { get; set; } = "standard";

        public bool AutoCopyToClipboard { get; set; } = true;
    }
}
