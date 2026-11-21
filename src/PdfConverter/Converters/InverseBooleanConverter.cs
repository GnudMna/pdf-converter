using System;
using System.Globalization;
using System.Windows.Data;

namespace PdfConverter.Converters
{
    /// <summary>
    /// <c>bool</c> 値を反転する WPF Value コンバーター
    /// </summary>
    /// <remarks>
    /// <c>IsEnabled</c> の逆をバインドするなど、UI 側で否定を表現する際に使用する
    /// </remarks>
    public class InverseBooleanConverter : IValueConverter
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }

        /// <inheritdoc/>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }
    }
}
