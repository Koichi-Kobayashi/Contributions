using System.ComponentModel;

namespace Contributions.Resources
{
    /// <summary>
    /// 翻訳リソース変更の通知源。
    /// </summary>
    public sealed class TranslationSource : INotifyPropertyChanged
    {
        public static TranslationSource Instance { get; } = new();

        /// <summary>
        /// 外部からの生成を防止する。
        /// </summary>
        private TranslationSource()
        {
        }

        /// <summary>
        /// 指定キーの翻訳文字列を取得する。
        /// </summary>
        public string this[string key] => Translations.GetString(key);

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 言語変更を通知する。
        /// </summary>
        internal void RaiseLanguageChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }
    }
}
