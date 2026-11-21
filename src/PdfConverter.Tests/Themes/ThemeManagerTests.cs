using System.Windows.Media;
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
        /// 保存済み設定の読み込みが ParseThemeMode と同じ変換規則を使うことを検証する
        /// </summary>
        [Fact]
        public void LoadSavedThemeMode_UsesSavedSettingValue()
        {
            var expected = ThemeManager.ParseThemeMode(PdfConverter.Properties.Settings.Default.ThemeMode);
            Assert.Equal(expected, ThemeManager.LoadSavedThemeMode());
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

        /// <summary>
        /// WPF の色が COLORREF (0x00BBGGRR) の並びに変換されることを検証する
        /// </summary>
        [Theory]
        [InlineData(0x11, 0x22, 0x33, 0x332211)]
        [InlineData(0xFF, 0x00, 0x00, 0x0000FF)]
        [InlineData(0x0B, 0x11, 0x20, 0x20110B)]
        public void ToColorRef_ReturnsBlueGreenRedOrder(byte r, byte g, byte b, int expected)
        {
            Assert.Equal(expected, WindowTitleBarHelper.ToColorRef(Color.FromRgb(r, g, b)));
        }
    }
}
