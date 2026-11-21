using PdfConverter.ViewModels;
using Xunit;

namespace PdfConverter.Tests.ViewModels
{
    /// <summary>
    /// <see cref="ThemeSettingsViewModel"/> のテーマ切替を検証する
    /// </summary>
    public class ThemeSettingsViewModelTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 3 種類のテーマ選択肢が公開されることを検証する
        /// </summary>
        [Fact]
        public void ThemeModeOptions_ContainsThreeModes()
        {
            var viewModel = new ThemeSettingsViewModel();

            Assert.Equal(3, viewModel.ThemeModeOptions.Count);
        }

        /// <summary>
        /// 初期状態では保存済み設定から読み込んだ ThemeMode と選択状態が一致することを検証する
        /// </summary>
        [Fact]
        public void ThemeMode_InitialSelectionFlagsMatchMode()
        {
            var viewModel = new ThemeSettingsViewModel();

            switch (viewModel.ThemeMode)
            {
                case Models.ThemeMode.Light:
                    Assert.True(viewModel.IsLightThemeSelected);
                    break;
                case Models.ThemeMode.Dark:
                    Assert.True(viewModel.IsDarkThemeSelected);
                    break;
                default:
                    Assert.True(viewModel.IsSystemThemeSelected);
                    break;
            }
        }
    }
}
