using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using PdfConverter.Services;
using PdfConverter.Tests.Helpers;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="DocumentPdfSourceService"/> の動作を検証する
    /// </summary>
    public class DocumentPdfSourceServiceTests
    {
        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        private static DocumentPdfSourceService CreateService(
            Mock<IWordToPdfConversionService> wordToPdf,
            Mock<IWordToPdfConversionSettings> settings = null)
        {
            settings = settings ?? WordToPdfConversionSettingsTestHelper.Create();
            return new DocumentPdfSourceService(wordToPdf.Object, settings.Object);
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// PDF 入力の場合は元ファイルパスをそのまま返すことを検証する
        /// </summary>
        [Fact]
        public async Task GetPdfPathAsync_PdfInput_ReturnsSourcePath()
        {
            var wordToPdf = new Mock<IWordToPdfConversionService>();
            var service = CreateService(wordToPdf);
            string path = CreateTempPdfPath();

            try
            {
                string result = await service.GetPdfPathAsync(path, CancellationToken.None);

                Assert.Equal(path, result);
                wordToPdf.Verify(
                    w => w.ConvertToPdfAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                    Times.Never);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Word 入力の場合は Word → PDF 変換サービスを呼び出すことを検証する
        /// </summary>
        [Fact]
        public async Task GetPdfPathAsync_WordInput_UsesWordToPdfService()
        {
            var wordToPdf = new Mock<IWordToPdfConversionService>();
            var service = CreateService(wordToPdf);
            string wordPath = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.docx");
            string pdfPath = CreateTempPdfPath();
            File.WriteAllText(wordPath, "placeholder");

            wordToPdf
                .Setup(w => w.ConvertToPdfAsync(wordPath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(pdfPath);

            try
            {
                string result = await service.GetPdfPathAsync(wordPath, CancellationToken.None);

                Assert.Equal(pdfPath, result);
                wordToPdf.Verify(w => w.ConvertToPdfAsync(wordPath, It.IsAny<CancellationToken>()), Times.Once);
            }
            finally
            {
                File.Delete(wordPath);
                File.Delete(pdfPath);
            }
        }

        /// <summary>
        /// 同一 Word ファイルへの 2 回目のアクセスではキャッシュを利用することを検証する
        /// </summary>
        [Fact]
        public async Task GetPdfPathAsync_WordInput_SecondCallUsesCache()
        {
            var wordToPdf = new Mock<IWordToPdfConversionService>();
            var service = CreateService(wordToPdf);
            string wordPath = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.docx");
            string pdfPath = CreateTempPdfPath();
            File.WriteAllText(wordPath, "placeholder");

            wordToPdf
                .Setup(w => w.ConvertToPdfAsync(wordPath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(pdfPath);

            try
            {
                await service.GetPdfPathAsync(wordPath, CancellationToken.None);
                await service.GetPdfPathAsync(wordPath, CancellationToken.None);

                wordToPdf.Verify(w => w.ConvertToPdfAsync(wordPath, It.IsAny<CancellationToken>()), Times.Once);
            }
            finally
            {
                File.Delete(wordPath);
                File.Delete(pdfPath);
            }
        }

        /// <summary>
        /// Dispose 時にキャッシュ済みの一時 PDF が削除されることを検証する
        /// </summary>
        [Fact]
        public async Task Dispose_RemovesCachedTemporaryPdf()
        {
            var wordToPdf = new Mock<IWordToPdfConversionService>();
            var service = CreateService(wordToPdf);
            string wordPath = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.docx");
            string pdfPath = CreateTempPdfPath();
            File.WriteAllText(wordPath, "placeholder");

            wordToPdf
                .Setup(w => w.ConvertToPdfAsync(wordPath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(pdfPath);

            try
            {
                await service.GetPdfPathAsync(wordPath, CancellationToken.None);
                Assert.True(File.Exists(pdfPath));

                service.Dispose();

                Assert.False(File.Exists(pdfPath));
            }
            finally
            {
                File.Delete(wordPath);
                if (File.Exists(pdfPath))
                {
                    File.Delete(pdfPath);
                }
            }
        }

        private static string CreateTempPdfPath()
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.pdf");
            File.WriteAllText(path, "placeholder");
            return path;
        }
    }
}
