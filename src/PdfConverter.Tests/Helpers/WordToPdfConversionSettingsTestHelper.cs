using Moq;
using PdfConverter.Models;
using PdfConverter.Services;

namespace PdfConverter.Tests.Helpers
{
    /// <summary>
    /// <see cref="IWordToPdfConversionSettings"/>のテスト用モック生成ヘルパー
    /// </summary>
    internal static class WordToPdfConversionSettingsTestHelper
    {
        /// <summary>
        /// 既定設定のモックを生成する
        /// </summary>
        public static Mock<IWordToPdfConversionSettings> Create()
        {
            var mock = new Mock<IWordToPdfConversionSettings>();
            mock.SetupGet(s => s.Backend).Returns(WordToPdfBackend.MicrosoftWord);
            mock.SetupGet(s => s.LibreOfficePath).Returns(string.Empty);
            mock.SetupSet(s => s.Backend = It.IsAny<WordToPdfBackend>());
            mock.SetupSet(s => s.LibreOfficePath = It.IsAny<string>());
            return mock;
        }
    }
}
