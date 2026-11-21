using Moq;
using PdfConverter.Models;
using PdfConverter.Services;
using PdfConverter.Tests.Helpers;
using PdfConverter.ViewModels;
using Xunit;

namespace PdfConverter.Tests.ViewModels
{
    /// <summary>
    /// <see cref="ImageExportSettingsViewModel"/> の出力設定ロジックを検証する
    /// </summary>
    public class ImageExportSettingsViewModelTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 出力形式を JPEG に変更したときに透明度保持が自動的に無効化されることを検証する
        /// </summary>
        [Fact]
        public void OutputImageFormat_Jpeg_DisablesPreserveTransparency()
        {
            var viewModel = CreateViewModel();
            viewModel.OutputImageFormat = OutputImageFormat.Png;
            viewModel.PreserveTransparency = true;

            viewModel.OutputImageFormat = OutputImageFormat.Jpeg;

            Assert.False(viewModel.PreserveTransparency);
            Assert.False(viewModel.IsTransparencySelectable);
        }

        /// <summary>
        /// 出力形式を PNG に設定したときに透明度の選択が可能であることを検証する
        /// </summary>
        [Fact]
        public void OutputImageFormat_Png_AllowsTransparency()
        {
            var viewModel = CreateViewModel();

            viewModel.OutputImageFormat = OutputImageFormat.Png;

            Assert.True(viewModel.IsTransparencySelectable);
        }

        /// <summary>
        /// ページ範囲を変更したときに検証メッセージがクリアされることを検証する
        /// </summary>
        [Fact]
        public void PageRange_Change_ClearsValidationMessage()
        {
            var viewModel = CreateViewModel();
            viewModel.PageRangeValidationMessage = "error";

            viewModel.PageRange = "1-2";

            Assert.Null(viewModel.PageRangeValidationMessage);
        }

        /// <summary>
        /// 保存済み設定から初期値が読み込まれることを検証する
        /// </summary>
        [Fact]
        public void Constructor_LoadsPersistedSettings()
        {
            var settings = ImageExportSettingsTestHelper.Create();
            settings.SetupGet(s => s.OutputImageFormat).Returns(OutputImageFormat.Jpeg);
            settings.SetupGet(s => s.ResolutionMode).Returns(ResolutionMode.Dpi);
            settings.SetupGet(s => s.ResolutionValue).Returns("300");
            settings.SetupGet(s => s.PreserveTransparency).Returns(true);

            var viewModel = new ImageExportSettingsViewModel(settings.Object, null, null);

            Assert.Equal(OutputImageFormat.Jpeg, viewModel.OutputImageFormat);
            Assert.Equal(ResolutionMode.Dpi, viewModel.ResolutionMode);
            Assert.Equal("300", viewModel.ResolutionValue);
            Assert.True(viewModel.PreserveTransparency);
            Assert.Equal("1", viewModel.PageRange);
            Assert.False(viewModel.IsAllPagesSelected);
        }

        /// <summary>
        /// 出力形式変更時に設定が保存されることを検証する
        /// </summary>
        [Fact]
        public void OutputImageFormat_Change_PersistsSettings()
        {
            var settings = ImageExportSettingsTestHelper.Create();
            var viewModel = new ImageExportSettingsViewModel(settings.Object, null, null);

            viewModel.OutputImageFormat = OutputImageFormat.Bmp;

            settings.VerifySet(s => s.OutputImageFormat = OutputImageFormat.Bmp);
            settings.Verify(s => s.Save(), Times.Once);
        }

        /// <summary>
        /// ページ範囲変更時には設定が保存されないことを検証する
        /// </summary>
        [Fact]
        public void PageRange_Change_DoesNotPersistSettings()
        {
            var settings = ImageExportSettingsTestHelper.Create();
            var viewModel = new ImageExportSettingsViewModel(settings.Object, null, null);

            viewModel.PageRange = "1-3";

            settings.Verify(s => s.Save(), Times.Never);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/

        private static ImageExportSettingsViewModel CreateViewModel()
        {
            return new ImageExportSettingsViewModel(ImageExportSettingsTestHelper.Create().Object, null, null);
        }
    }
}
