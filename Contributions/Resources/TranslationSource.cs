using System.ComponentModel;

namespace Contributions.Resources
{
    public sealed class TranslationSource : INotifyPropertyChanged
    {
        public static TranslationSource Instance { get; } = new();

        private TranslationSource()
        {
        }

        public string this[string key] => Translations.GetString(key);

        public event PropertyChangedEventHandler? PropertyChanged;

        internal void RaiseLanguageChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }
    }
}
