using FluentAssertions;
using Moq;
using PdfConverter.Models;
using PdfConverter.Services;
using PdfConverter.Tests.Helpers;
using PdfConverter.ViewModels.Coordinators;
using Xunit;

namespace PdfConverter.Tests.ViewModels.Coordinators
{
    /// <summary>
    /// <see cref="CoordinatorHelpers"/> の解像度入力検証ヘルパーを検証する
    /// </summary>
    public class CoordinatorHelpersTests
    {
        /// <summary>
        /// 有効な解像度設定で検証が成功し、ダイアログが表示されないことを検証する
        /// </summary>
        [Fact]
        public void TryGetResolutionValue_ValidInput_ReturnsTrue()
        {
            var host = new TestMainViewModelHost
            {
                ResolutionMode = ResolutionMode.Width,
                ResolutionValue = "1080",
            };
            var dialog = new Mock<IDialogService>();

            var success = CoordinatorHelpers.TryGetResolutionValue(host, dialog.Object, out double value, showDialog: false);

            success.Should().BeTrue();
            value.Should().Be(1080);
            dialog.Verify(d => d.ShowMessage(It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// 無効な解像度設定で検証が失敗し、ステータスメッセージが設定され、showDialog 時にダイアログが表示されることを検証する
        /// </summary>
        [Fact]
        public void TryGetResolutionValue_InvalidInput_SetsStatusAndShowsDialogWhenRequested()
        {
            var host = new TestMainViewModelHost
            {
                ResolutionMode = ResolutionMode.Dpi,
                ResolutionValue = "",
            };
            var dialog = new Mock<IDialogService>();

            var success = CoordinatorHelpers.TryGetResolutionValue(host, dialog.Object, out _, showDialog: true);

            success.Should().BeFalse();
            host.StatusMessage.Should().NotBeNullOrWhiteSpace();
            dialog.Verify(d => d.ShowMessage(host.StatusMessage), Times.Once);
        }
    }
}
