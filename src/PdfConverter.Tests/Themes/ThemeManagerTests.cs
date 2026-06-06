using FluentAssertions;
using PdfConverter.Models;
using PdfConverter.Themes;
using Xunit;

namespace PdfConverter.Tests.Themes
{
    /// <summary>
    /// <see cref="ThemeManager.ParseThemeMode"/> の設定文字列パースを検証する
    /// </summary>
    public class ThemeManagerTests
    {
        /// <summary>
        /// 有効なテーマ名文字列が対応する ThemeMode に変換されることを検証する
        /// </summary>
        [Theory]
        [InlineData("Light", ThemeMode.Light)]
        [InlineData("Dark", ThemeMode.Dark)]
        [InlineData("System", ThemeMode.System)]
        public void ParseThemeMode_ValidValue_ReturnsParsedMode(string input, ThemeMode expected)
        {
            ThemeManager.ParseThemeMode(input).Should().Be(expected);
        }

        /// <summary>
        /// null・空文字・不正な文字列の場合に ThemeMode.System がフォールバックとして返されることを検証する
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("invalid")]
        public void ParseThemeMode_InvalidValue_ReturnsSystem(string input)
        {
            ThemeManager.ParseThemeMode(input).Should().Be(ThemeMode.System);
        }
    }
}
