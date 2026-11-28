using Moq;
using PdfConverter.Models;
using PdfConverter.Services;

namespace PdfConverter.Tests.Helpers
{
    /// <summary>
    /// <see cref="IImageExportSettings"/> のテスト用モック生成ヘルパー
    /// </summary>
    internal static class ImageExportSettingsTestHelper
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 既定値を持つ <see cref="IImageExportSettings"/> モックを生成する
        /// </summary>
        public static Mock<IImageExportSettings> Create()
        {
            var mock = new Mock<IImageExportSettings>();
            mock.SetupGet(s => s.OutputImageFormat).Returns(OutputImageFormat.Png);
            mock.SetupGet(s => s.ResolutionMode).Returns(ResolutionMode.Width);
            mock.SetupGet(s => s.ResolutionValue).Returns("1080");
            mock.SetupGet(s => s.PreserveTransparency).Returns(false);
            mock.SetupGet(s => s.JpegQuality).Returns(90);
            mock.SetupSet(s => s.OutputImageFormat = It.IsAny<OutputImageFormat>());
            mock.SetupSet(s => s.ResolutionMode = It.IsAny<ResolutionMode>());
            mock.SetupSet(s => s.ResolutionValue = It.IsAny<string>());
            mock.SetupSet(s => s.PreserveTransparency = It.IsAny<bool>());
            mock.SetupSet(s => s.JpegQuality = It.IsAny<int>());
            mock.Setup(s => s.Reload());
            return mock;
        }
    }
}
