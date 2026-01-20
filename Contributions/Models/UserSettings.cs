namespace Contributions.Models
{
    public class UserSettings
    {
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
