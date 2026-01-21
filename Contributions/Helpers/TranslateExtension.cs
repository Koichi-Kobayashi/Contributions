using System;
using System.Windows.Data;
using System.Windows.Markup;
using Contributions.Resources;

namespace Contributions.Helpers
{
    /// <summary>
    /// 翻訳リソースにバインドするためのマークアップ拡張。
    /// </summary>
    [MarkupExtensionReturnType(typeof(string))]
    public class TranslateExtension : MarkupExtension
    {
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// 指定キーの翻訳文字列を返すバインディングを構築する。
        /// </summary>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var binding = new Binding($"[{Key}]")
            {
                Source = TranslationSource.Instance,
                Mode = BindingMode.OneWay
            };

            return binding.ProvideValue(serviceProvider);
        }
    }
}
