using Microsoft.Win32;
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
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 有効なテーマ名文字列が対応する ThemeMode に変換されることを検証する
        /// </summary>
        [Theory]
        [InlineData("Light", ThemeMode.Light)]
        [InlineData("Dark", ThemeMode.Dark)]
        [InlineData("System", ThemeMode.System)]
        public void ParseThemeMode_ValidValue_ReturnsParsedMode(string input, ThemeMode expected)
        {
            Assert.Equal(expected, ThemeManager.ParseThemeMode(input));
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
            Assert.Equal(ThemeMode.System, ThemeManager.ParseThemeMode(input));
        }

        /// <summary>
        /// システム追従モードかつ一般設定変更時のみテーマ再適用対象になることを検証する
        /// </summary>
        [Theory]
        [InlineData(ThemeMode.System, UserPreferenceCategory.General, true)]
        [InlineData(ThemeMode.Light, UserPreferenceCategory.General, false)]
        [InlineData(ThemeMode.Dark, UserPreferenceCategory.General, false)]
        [InlineData(ThemeMode.System, UserPreferenceCategory.Color, false)]
        public void ShouldReactToUserPreferenceChange_ReturnsExpected(
            ThemeMode selectedMode,
            UserPreferenceCategory category,
            bool expected)
        {
            Assert.Equal(expected, ThemeManager.ShouldReactToUserPreferenceChange(selectedMode, category));
        }
    }
}
