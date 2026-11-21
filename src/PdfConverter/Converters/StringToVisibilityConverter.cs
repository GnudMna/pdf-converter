using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PdfConverter.Converters
{
    /// <summary>
    /// 文字列が空でないとき <see cref="Visibility.Visible"/> を返す WPF Value コンバーター
    /// </summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <inheritdoc/>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
