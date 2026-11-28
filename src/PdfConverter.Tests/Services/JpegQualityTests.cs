using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="JpegQuality"/> の範囲検証・解析を検証する
    /// </summary>
    public class JpegQualityTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 1〜100 の値だけが有効と判定されることを検証する
        /// </summary>
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [InlineData(90, true)]
        [InlineData(100, true)]
        [InlineData(101, false)]
        [InlineData(-5, false)]
        public void IsValid_Value_ReturnsExpected(int quality, bool expected)
        {
            Assert.Equal(expected, JpegQuality.IsValid(quality));
        }

        /// <summary>
        /// 範囲外の値が範囲内に丸められることを検証する
        /// </summary>
        [Theory]
        [InlineData(0, 1)]
        [InlineData(55, 55)]
        [InlineData(150, 100)]
        public void Clamp_Value_ReturnsClampedValue(int quality, int expected)
        {
            Assert.Equal(expected, JpegQuality.Clamp(quality));
        }

        /// <summary>
        /// 不正な保存値は既定値にフォールバックすることを検証する
        /// </summary>
        [Theory]
        [InlineData(0, JpegQuality.Default)]
        [InlineData(101, JpegQuality.Default)]
        [InlineData(75, 75)]
        public void Resolve_StoredValue_ReturnsValidValue(int stored, int expected)
        {
            Assert.Equal(expected, JpegQuality.Resolve(stored));
        }

        /// <summary>
        /// 整数かつ範囲内の文字列だけが解析できることを検証する
        /// </summary>
        [Theory]
        [InlineData("85", true, 85)]
        [InlineData(" 1 ", true, 1)]
        [InlineData("100", true, 100)]
        [InlineData("0", false, 0)]
        [InlineData("101", false, 101)]
        [InlineData("-1", false, 0)]
        [InlineData("9.5", false, 0)]
        [InlineData("abc", false, 0)]
        [InlineData("", false, 0)]
        [InlineData(null, false, 0)]
        public void TryParse_Text_ReturnsExpected(string text, bool expected, int expectedQuality)
        {
            bool result = JpegQuality.TryParse(text, out int quality);

            Assert.Equal(expected, result);
            if (expected)
            {
                Assert.Equal(expectedQuality, quality);
            }
        }
    }
}
