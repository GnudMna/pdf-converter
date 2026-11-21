using Moq;
using PdfConverter.Models;
using PdfConverter.Services;

namespace PdfConverter.Tests.Helpers
{
    /// <summary>
    /// <see cref="IWordToPdfConversionSettings"/> のテスト用モック生成ヘルパー
    /// </summary>
    internal static class WordToPdfConversionSettingsTestHelper
    {
        /// <summary>
        /// 既定設定のモックを生成する
        /// </summary>
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        public static Mock<IWordToPdfConversionSettings> Create()
        {
            var mock = new Mock<IWordToPdfConversionSettings>();
            mock.SetupGet(s => s.Backend).Returns(WordToPdfBackend.MicrosoftWord);
            mock.SetupGet(s => s.LibreOfficePath).Returns(string.Empty);
            mock.SetupGet(s => s.PdfFormat).Returns(WordToPdfPdfFormat.Standard);
            mock.SetupGet(s => s.OptimizeFor).Returns(WordToPdfOptimizeFor.Print);
            mock.SetupGet(s => s.ExportBookmarks).Returns(true);
            mock.SetupGet(s => s.ExportComments).Returns(false);
            mock.SetupSet(s => s.Backend = It.IsAny<WordToPdfBackend>());
            mock.SetupSet(s => s.LibreOfficePath = It.IsAny<string>());
            mock.SetupSet(s => s.PdfFormat = It.IsAny<WordToPdfPdfFormat>());
            mock.SetupSet(s => s.OptimizeFor = It.IsAny<WordToPdfOptimizeFor>());
            mock.SetupSet(s => s.ExportBookmarks = It.IsAny<bool>());
            mock.SetupSet(s => s.ExportComments = It.IsAny<bool>());
            return mock;
        }
    }
}
