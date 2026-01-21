using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Appearance;

namespace Contributions.Helpers
{
    /// <summary>
    /// Enum値と一致するかをboolに変換するコンバーター。
    /// </summary>
    internal class EnumToBooleanConverter : IValueConverter
    {
        /// <summary>
        /// 指定されたEnum名と一致するかを判定する。
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is not String enumString)
            {
                throw new ArgumentException("ExceptionEnumToBooleanConverterParameterMustBeAnEnumName");
            }

            if (!Enum.IsDefined(typeof(ApplicationTheme), value))
            {
                throw new ArgumentException("ExceptionEnumToBooleanConverterValueMustBeAnEnum");
            }

            var enumValue = Enum.Parse(typeof(ApplicationTheme), enumString);

            return enumValue.Equals(value);
        }

        /// <summary>
        /// 指定されたEnum名をEnum値に変換する。
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is not String enumString)
            {
                throw new ArgumentException("ExceptionEnumToBooleanConverterParameterMustBeAnEnumName");
            }

            return Enum.Parse(typeof(ApplicationTheme), enumString);
        }
    }
}
