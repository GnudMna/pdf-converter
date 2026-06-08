using FluentAssertions;
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

            viewModel.PreserveTransparency.Should().BeFalse();
            viewModel.IsTransparencySelectable.Should().BeFalse();
        }

        /// <summary>
        /// 出力形式を PNG に設定したときに透明度の選択が可能であることを検証する
        /// </summary>
        [Fact]
        public void OutputImageFormat_Png_AllowsTransparency()
        {
            var viewModel = CreateViewModel();

            viewModel.OutputImageFormat = OutputImageFormat.Png;

            viewModel.IsTransparencySelectable.Should().BeTrue();
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

            viewModel.PageRangeValidationMessage.Should().BeNull();
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

            viewModel.OutputImageFormat.Should().Be(OutputImageFormat.Jpeg);
            viewModel.ResolutionMode.Should().Be(ResolutionMode.Dpi);
            viewModel.ResolutionValue.Should().Be("300");
            viewModel.PreserveTransparency.Should().BeTrue();
            viewModel.PageRange.Should().Be("1");
            viewModel.IsAllPagesSelected.Should().BeFalse();
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

        private static ImageExportSettingsViewModel CreateViewModel()
        {
            return new ImageExportSettingsViewModel(ImageExportSettingsTestHelper.Create().Object, null, null);
        }
    }
}
