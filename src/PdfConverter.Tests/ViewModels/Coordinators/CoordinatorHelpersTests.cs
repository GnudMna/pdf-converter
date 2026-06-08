using FluentAssertions;
using PdfConverter.Models;
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
        /// 有効な解像度設定で検証が成功し、検証メッセージがクリアされることを検証する
        /// </summary>
        [Fact]
        public void TryGetResolutionValue_ValidInput_ReturnsTrue()
        {
            var host = new TestMainViewModelHost
            {
                ResolutionMode = ResolutionMode.Width,
                ResolutionValue = "1080",
                ResolutionValidationMessage = "previous error",
            };

            var success = CoordinatorHelpers.TryGetResolutionValue(host, host, out double value, showFieldValidation: false);

            success.Should().BeTrue();
            value.Should().Be(1080);
            host.ResolutionValidationMessage.Should().BeNull();
        }

        /// <summary>
        /// 無効な解像度設定で検証が失敗し、ステータスと入力欄の検証メッセージが設定されることを検証する
        /// </summary>
        [Fact]
        public void TryGetResolutionValue_InvalidInput_SetsStatusAndFieldValidationWhenRequested()
        {
            var host = new TestMainViewModelHost
            {
                ResolutionMode = ResolutionMode.Dpi,
                ResolutionValue = "",
            };

            var success = CoordinatorHelpers.TryGetResolutionValue(host, host, out _, showFieldValidation: true);

            success.Should().BeFalse();
            host.StatusMessage.Should().NotBeNullOrWhiteSpace();
            host.StatusKind.Should().Be(StatusKind.Warning);
            host.ResolutionValidationMessage.Should().Be(host.StatusMessage);
        }
    }
}
