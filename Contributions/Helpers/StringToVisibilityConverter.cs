using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Contributions.Helpers
{
    /// <summary>
    /// 文字列の有無に応じて表示/非表示を切り替えるコンバーター。
    /// </summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// 空文字の場合はCollapsed、それ以外はVisibleを返す。
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value as string;
            return string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>
        /// 逆変換はサポートしない。
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
