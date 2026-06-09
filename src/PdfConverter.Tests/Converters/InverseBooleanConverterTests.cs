using System.Globalization;
using PdfConverter.Converters;
using Xunit;

namespace PdfConverter.Tests.Converters
{
    /// <summary>
    /// <see cref="InverseBooleanConverter"/> の真偽値反転変換を検証する
    /// </summary>
    public class InverseBooleanConverterTests
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        private readonly InverseBooleanConverter _converter = new InverseBooleanConverter();


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/

        /// <summary>
        /// Convert で bool 値が反転されることを検証する
        /// </summary>
        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void Convert_InvertsBoolean(bool input, bool expected)
        {
            Assert.Equal(expected, _converter.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Convert に bool 以外の値を渡した場合に false が返されることを検証する
        /// </summary>
        [Fact]
        public void Convert_NonBoolean_ReturnsFalse()
        {
            Assert.Equal(false, _converter.Convert("invalid", typeof(bool), null, CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// ConvertBack で bool 値が反転されることを検証する
        /// </summary>
        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void ConvertBack_InvertsBoolean(bool input, bool expected)
        {
            Assert.Equal(expected, _converter.ConvertBack(input, typeof(bool), null, CultureInfo.InvariantCulture));
        }
    }
}
