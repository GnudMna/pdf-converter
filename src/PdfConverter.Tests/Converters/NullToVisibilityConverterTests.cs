using System;
using System.Globalization;
using System.Windows;
using PdfConverter.Converters;
using Xunit;

namespace PdfConverter.Tests.Converters
{
    /// <summary>
    /// <see cref="NullToVisibilityConverter"/> の null 判定による Visibility 変換を検証する
    /// </summary>
    public class NullToVisibilityConverterTests
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        private readonly NullToVisibilityConverter _converter = new NullToVisibilityConverter();


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/

        /// <summary>
        /// バインド値が null のとき Visible が返されることを検証する (プレースホルダー表示用)
        /// </summary>
        [Fact]
        public void Convert_Null_ReturnsVisible()
        {
            Assert.Equal(Visibility.Visible, _converter.Convert(null, typeof(Visibility), null, CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// バインド値が非 null のとき Collapsed が返されることを検証する
        /// </summary>
        [Fact]
        public void Convert_NonNull_ReturnsCollapsed()
        {
            Assert.Equal(Visibility.Collapsed, _converter.Convert("value", typeof(Visibility), null, CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// ConvertBack が未サポートであり NotSupportedException がスローされることを検証する
        /// </summary>
        [Fact]
        public void ConvertBack_ThrowsNotSupportedException()
        {
            Action act = () => _converter.ConvertBack(Visibility.Visible, typeof(object), null, CultureInfo.InvariantCulture);

            Assert.Throws<NotSupportedException>(act);
        }
    }
}
