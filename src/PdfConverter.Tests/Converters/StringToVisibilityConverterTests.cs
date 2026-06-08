using System;
using System.Globalization;
using System.Windows;
using FluentAssertions;
using PdfConverter.Converters;
using Xunit;

namespace PdfConverter.Tests.Converters
{
    /// <summary>
    /// <see cref="StringToVisibilityConverter"/> の空文字列判定による Visibility 変換を検証する
    /// </summary>
    public class StringToVisibilityConverterTests
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        private readonly StringToVisibilityConverter _converter = new StringToVisibilityConverter();


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/

        /// <summary>
        /// null・空文字・空白のみのとき Collapsed が返されることを検証する
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Convert_EmptyOrWhitespace_ReturnsCollapsed(string value)
        {
            _converter.Convert(value, typeof(Visibility), null, CultureInfo.InvariantCulture)
                .Should().Be(Visibility.Collapsed);
        }

        /// <summary>
        /// 非空文字列のとき Visible が返されることを検証する
        /// </summary>
        [Fact]
        public void Convert_NonEmptyString_ReturnsVisible()
        {
            _converter.Convert("error message", typeof(Visibility), null, CultureInfo.InvariantCulture)
                .Should().Be(Visibility.Visible);
        }

        /// <summary>
        /// 文字列以外の値は Collapsed として扱われることを検証する
        /// </summary>
        [Fact]
        public void Convert_NonStringValue_ReturnsCollapsed()
        {
            _converter.Convert(42, typeof(Visibility), null, CultureInfo.InvariantCulture)
                .Should().Be(Visibility.Collapsed);
        }

        /// <summary>
        /// ConvertBack が未サポートであり NotSupportedException がスローされることを検証する
        /// </summary>
        [Fact]
        public void ConvertBack_ThrowsNotSupportedException()
        {
            Action act = () => _converter.ConvertBack(Visibility.Visible, typeof(string), null, CultureInfo.InvariantCulture);

            act.Should().Throw<NotSupportedException>();
        }
    }
}
