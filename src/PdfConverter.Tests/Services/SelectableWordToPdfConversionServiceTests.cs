using System.Threading;
using System.Threading.Tasks;
using Moq;
using PdfConverter.Models;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="SelectableWordToPdfConversionService"/> の動作を検証する
    /// </summary>
    public class SelectableWordToPdfConversionServiceTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// Microsoft Word 設定時は Word COM サービスへ委譲することを検証する
        /// </summary>
        [Fact]
        public void ConvertToPdfAsync_MicrosoftWord_DelegatesToWordService()
        {
            var settings = new Mock<IWordToPdfConversionSettings>();
            settings.SetupGet(s => s.Backend).Returns(WordToPdfBackend.MicrosoftWord);

            var microsoftWord = new Mock<IWordToPdfConversionService>();
            var libreOffice = new Mock<IWordToPdfConversionService>(MockBehavior.Strict);
            var service = new SelectableWordToPdfConversionService(
                microsoftWord.Object,
                libreOffice.Object,
                settings.Object);

            service.ConvertToPdfAsync("C:\\docs\\sample.docx");

            microsoftWord.Verify(
                s => s.ConvertToPdfAsync("C:\\docs\\sample.docx", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        /// <summary>
        /// LibreOffice 設定時は LibreOffice サービスへ委譲することを検証する
        /// </summary>
        [Fact]
        public void ConvertToPdfAsync_LibreOffice_DelegatesToLibreOfficeService()
        {
            var settings = new Mock<IWordToPdfConversionSettings>();
            settings.SetupGet(s => s.Backend).Returns(WordToPdfBackend.LibreOffice);

            var microsoftWord = new Mock<IWordToPdfConversionService>(MockBehavior.Strict);
            var libreOffice = new Mock<IWordToPdfConversionService>();
            var service = new SelectableWordToPdfConversionService(
                microsoftWord.Object,
                libreOffice.Object,
                settings.Object);

            service.ConvertToPdfAsync("C:\\docs\\sample.docx");

            libreOffice.Verify(
                s => s.ConvertToPdfAsync("C:\\docs\\sample.docx", It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
