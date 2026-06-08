using FluentAssertions;
using PdfConverter.Models;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="ImageExportSettings"/> の設定解決ロジックを検証する
    /// </summary>
    public class ImageExportSettingsTests
    {
        /// <summary>
        /// 有効な解像度値はそのまま採用されることを検証する
        /// </summary>
        [Fact]
        public void ResolveResolutionValue_ValidValue_ReturnsStoredValue()
        {
            ImageExportSettings.ResolveResolutionValue(ResolutionMode.Width, " 1920 ")
                .Should().Be("1920");
        }

        /// <summary>
        /// 無効な解像度値はモード別の既定値にフォールバックすることを検証する
        /// </summary>
        [Theory]
        [InlineData(ResolutionMode.Width, "1080")]
        [InlineData(ResolutionMode.Height, "1920")]
        [InlineData(ResolutionMode.Dpi, "150")]
        public void ResolveResolutionValue_InvalidValue_ReturnsDefault(ResolutionMode mode, string expected)
        {
            ImageExportSettings.ResolveResolutionValue(mode, "invalid")
                .Should().Be(expected);
        }

        /// <summary>
        /// JPEG では透明度保持が無効化されることを検証する
        /// </summary>
        [Fact]
        public void ResolvePreserveTransparency_Jpeg_ReturnsFalse()
        {
            ImageExportSettings.ResolvePreserveTransparency(OutputImageFormat.Jpeg, storedValue: true)
                .Should().BeFalse();
        }

        /// <summary>
        /// PNG では保存済みの透明度保持設定が反映されることを検証する
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ResolvePreserveTransparency_Png_ReturnsStoredValue(bool storedValue)
        {
            ImageExportSettings.ResolvePreserveTransparency(OutputImageFormat.Png, storedValue)
                .Should().Be(storedValue);
        }
    }
}
