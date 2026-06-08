using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PdfConverter.Converters
{
    /// <summary>
    /// バインド値が <c>null</c> のとき <see cref="Visibility.Visible"/>、
    /// 非 <c>null</c> のとき <see cref="Visibility.Collapsed"/> を返す WPF Value コンバーター
    /// </summary>
    /// <remarks>
    /// 通常の<c>null</c>チェックとは可視性の論理が逆になっている点に注意
    /// </remarks>
    public class NullToVisibilityConverter : IValueConverter
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <inheritdoc/>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
