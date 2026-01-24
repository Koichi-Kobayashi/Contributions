namespace Contributions.Models
{
    /// <summary>
    /// アプリの永続化設定。
    /// </summary>
    public class UserSettings
    {
        public string ShareText { get; set; } = "My GitHub contributions this year 🚀";

        public string ShareUrlOption { get; set; } = string.Empty;

        public string ShareUrl { get; set; } = string.Empty;

        public string ShareHashtag1 { get; set; } = string.Empty;

        public string ShareHashtag2 { get; set; } = string.Empty;

        public string ShareHashtag3 { get; set; } = string.Empty;

        public string ShareHashtags { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;

        public string ThemeMode { get; set; } = "Dark";

        public string PaletteName { get; set; } = "standard";

        public bool AutoCopyToClipboard { get; set; } = true;

        public string Language { get; set; } = string.Empty;

        public string SelectedYear { get; set; } = string.Empty;

        public string SelectedYearKind { get; set; } = string.Empty;

        public string SelectedYearValue { get; set; } = string.Empty;
    }
}
