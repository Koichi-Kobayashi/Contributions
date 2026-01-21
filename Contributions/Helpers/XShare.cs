using System.Diagnostics;

namespace Contributions.Helpers
{
    /// <summary>
    /// X（旧Twitter）の共有画面を開くヘルパー。
    /// </summary>
    public static class XShare
    {
        /// <summary>
        /// ツイート作成画面を既定ブラウザで開く。
        /// </summary>
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
