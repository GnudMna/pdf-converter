using FluentAssertions;
using PdfConverter.Models;
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
            var viewModel = new ImageExportSettingsViewModel(null, null);
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
            var viewModel = new ImageExportSettingsViewModel(null, null);

            viewModel.OutputImageFormat = OutputImageFormat.Png;

            viewModel.IsTransparencySelectable.Should().BeTrue();
        }

        /// <summary>
        /// ページ範囲を変更したときに検証メッセージがクリアされることを検証する
        /// </summary>
        [Fact]
        public void PageRange_Change_ClearsValidationMessage()
        {
            var viewModel = new ImageExportSettingsViewModel(null, null);
            viewModel.PageRangeValidationMessage = "error";

            viewModel.PageRange = "1-2";

            viewModel.PageRangeValidationMessage.Should().BeNull();
        }
    }
}
