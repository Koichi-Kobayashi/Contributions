using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Xml.Linq;

namespace Contributions.Resources
{
    /// <summary>
    /// 埋め込み翻訳リソースを取得するユーティリティ。
    /// </summary>
    public static partial class Translations
    {
        private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> Cache = new();
        private static readonly Assembly Assembly = typeof(Translations).Assembly;
        private static readonly string[] FallbackCultures = ["en-US"];

        /// <summary>
        /// 指定のカルチャを適用して翻訳を更新する。
        /// </summary>
        public static void ApplyCulture(string? cultureName)
        {
            CultureInfo culture;
            try
            {
                culture = string.IsNullOrWhiteSpace(cultureName)
                    ? CultureInfo.InstalledUICulture
                    : CultureInfo.GetCultureInfo(cultureName);
            }
            catch (CultureNotFoundException)
            {
                culture = CultureInfo.InstalledUICulture;
            }

            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            TranslationSource.Instance.RaiseLanguageChanged();
        }

        /// <summary>
        /// 指定キーの翻訳文字列を取得する。
        /// </summary>
        public static string GetString(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;

            var value = GetValueForCulture(key, CultureInfo.CurrentUICulture);
            return string.IsNullOrWhiteSpace(value) ? key : value;
        }

        /// <summary>
        /// 翻訳文字列を指定引数でフォーマットする。
        /// </summary>
        public static string Format(string key, params object[] args)
        {
            return string.Format(CultureInfo.CurrentUICulture, GetString(key), args);
        }

        /// <summary>
        /// カルチャ階層とフォールバックを辿って翻訳値を取得する。
        /// </summary>
        private static string? GetValueForCulture(string key, CultureInfo culture)
        {
            var current = culture;
            while (!string.IsNullOrWhiteSpace(current.Name))
            {
                var value = GetFromResource(current.Name, key);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;

                current = current.Parent;
            }

            var neutral = GetFromResource(string.Empty, key);
            if (!string.IsNullOrWhiteSpace(neutral))
                return neutral;

            foreach (var fallback in FallbackCultures)
            {
                var value = GetFromResource(fallback, key);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return null;
        }

        /// <summary>
        /// 指定カルチャのリソースから翻訳値を取得する。
        /// </summary>
        private static string? GetFromResource(string cultureName, string key)
        {
            var resourceName = GetResourceName(cultureName);
            var map = Cache.GetOrAdd(resourceName, LoadResource);
            return map.TryGetValue(key, out var value) ? value : null;
        }

        /// <summary>
        /// カルチャ名から埋め込みリソース名を生成する。
        /// </summary>
        private static string GetResourceName(string cultureName)
        {
            if (string.IsNullOrWhiteSpace(cultureName))
                return "Contributions.Strings.Resources.resw";

            var normalized = cultureName.Replace('-', '_');
            return $"Contributions.Strings.{normalized}.Resources.resw";
        }

        /// <summary>
        /// 埋め込みリソースを読み込み、翻訳マップを生成する。
        /// </summary>
        private static IReadOnlyDictionary<string, string> LoadResource(string resourceName)
        {
            using var stream = Assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                return new Dictionary<string, string>();

            var doc = XDocument.Load(stream);
            var entries = doc.Root?
                .Elements("data")
                .Select(e => new
                {
                    Key = e.Attribute("name")?.Value,
                    Value = e.Element("value")?.Value
                })
                .Where(e => !string.IsNullOrWhiteSpace(e.Key))
                .ToDictionary(e => e.Key!, e => e.Value ?? string.Empty)
                ?? new Dictionary<string, string>();

            return entries;
        }
    }
}
