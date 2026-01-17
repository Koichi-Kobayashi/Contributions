using System.Diagnostics;

namespace Contributions.Helpers
{
    public static class XShare
    {
        public static void OpenTweetComposer(string text, string? url = null, string? hashtags = null)
        {
            var parameters = new List<string>();

            if (!string.IsNullOrWhiteSpace(text))
                parameters.Add("text=" + Uri.EscapeDataString(text));

            if (!string.IsNullOrWhiteSpace(url))
                parameters.Add("url=" + Uri.EscapeDataString(url));

            if (!string.IsNullOrWhiteSpace(hashtags))
                parameters.Add("hashtags=" + Uri.EscapeDataString(hashtags));

            var intent = "https://twitter.com/intent/tweet"
                + (parameters.Count > 0 ? "?" + string.Join("&", parameters) : "");

            Process.Start(new ProcessStartInfo
            {
                FileName = intent,
                UseShellExecute = true
            });
        }
    }
}
