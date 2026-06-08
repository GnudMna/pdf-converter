using FluentAssertions;
using PdfConverter.Models;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="ResolutionValueParser"/> のデフォルト値取得と解像度数値の検証・解析を検証する
    /// </summary>
    public class ResolutionValueParserTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 各解像度モードに対応するデフォルト値文字列が正しく返されることを検証する
        /// </summary>
        [Theory]
        [InlineData(ResolutionMode.Width, "1080")]
        [InlineData(ResolutionMode.Height, "1920")]
        [InlineData(ResolutionMode.Dpi, "150")]
        [InlineData(ResolutionMode.Default, "")]
        public void GetDefaultValue_ReturnsExpectedValue(ResolutionMode mode, string expected)
        {
            ResolutionValueParser.GetDefaultValue(mode).Should().Be(expected);
        }

        /// <summary>
        /// Default モードでは数値入力なしで検証が成功することを検証する
        /// </summary>
        [Fact]
        public void TryParse_DefaultMode_ReturnsTrueWithoutValue()
        {
            var success = ResolutionValueParser.TryParse(ResolutionMode.Default, "", out double value, out string error);

            success.Should().BeTrue();
            value.Should().Be(0);
            error.Should().BeNull();
        }

        /// <summary>
        /// 空文字・空白・null の入力で検証が失敗し、エラーメッセージが返されることを検証する
        /// </summary>
        [Theory]
        [InlineData(ResolutionMode.Width, "")]
        [InlineData(ResolutionMode.Height, "   ")]
        [InlineData(ResolutionMode.Dpi, null)]
        public void TryParse_EmptyValue_ReturnsFalse(ResolutionMode mode, string input)
        {
            var success = ResolutionValueParser.TryParse(mode, input, out _, out string error);

            success.Should().BeFalse();
            error.Should().NotBeNullOrWhiteSpace();
        }

        /// <summary>
        /// 数値に変換できない文字列で検証が失敗することを検証する
        /// </summary>
        [Theory]
        [InlineData(ResolutionMode.Width, "abc")]
        [InlineData(ResolutionMode.Dpi, "not-a-number")]
        public void TryParse_InvalidNumber_ReturnsFalse(ResolutionMode mode, string input)
        {
            var success = ResolutionValueParser.TryParse(mode, input, out _, out string error);

            success.Should().BeFalse();
            error.Should().Contain("数値");
        }

        /// <summary>
        /// 0 以下の値で検証が失敗することを検証する
        /// </summary>
        [Theory]
        [InlineData(ResolutionMode.Width, "0")]
        [InlineData(ResolutionMode.Dpi, "-10")]
        public void TryParse_NonPositiveValue_ReturnsFalse(ResolutionMode mode, string input)
        {
            var success = ResolutionValueParser.TryParse(mode, input, out _, out string error);

            success.Should().BeFalse();
            error.Should().Contain("0 より大きい");
        }

        /// <summary>
        /// DPI が上限 1200 を超える場合に検証が失敗することを検証する
        /// </summary>
        [Fact]
        public void TryParse_DpiAboveLimit_ReturnsFalse()
        {
            var success = ResolutionValueParser.TryParse(ResolutionMode.Dpi, "1201", out _, out string error);

            success.Should().BeFalse();
            error.Should().Contain("1200");
        }

        /// <summary>
        /// 幅・高さのピクセル値が上限 30000 を超える場合に検証が失敗することを検証する
        /// </summary>
        [Fact]
        public void TryParse_WidthAboveLimit_ReturnsFalse()
        {
            var success = ResolutionValueParser.TryParse(ResolutionMode.Width, "30001", out _, out string error);

            success.Should().BeFalse();
            error.Should().Contain("30000");
        }

        /// <summary>
        /// 有効な数値入力が正しく double に解析されることを検証する
        /// </summary>
        [Theory]
        [InlineData(ResolutionMode.Width, "1920", 1920)]
        [InlineData(ResolutionMode.Height, "1080.5", 1080.5)]
        [InlineData(ResolutionMode.Dpi, "150", 150)]
        public void TryParse_ValidValue_ReturnsParsedNumber(ResolutionMode mode, string input, double expected)
        {
            var success = ResolutionValueParser.TryParse(mode, input, out double value, out string error);

            success.Should().BeTrue();
            value.Should().Be(expected);
            error.Should().BeNull();
        }
    }
}
