using System;
using System.Windows.Markup;
using Contributions.Resources;

namespace Contributions.Helpers
{
    [MarkupExtensionReturnType(typeof(string))]
    public class TranslateExtension : MarkupExtension
    {
        public string Key { get; set; } = string.Empty;

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return Translations.GetString(Key);
        }
    }
}
