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
        /// Word ファイルが更新された場合は再変換し、古い一時 PDF を削除することを検証する
        /// </summary>
        [Fact]
        public async Task GetPdfPathAsync_WordInputModified_ReconvertsAndDeletesStalePdf()
        {
            var wordToPdf = new Mock<IWordToPdfConversionService>();
            var service = CreateService(wordToPdf);
            string wordPath = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.docx");
            string firstPdfPath = CreateTempPdfPath();
            string secondPdfPath = CreateTempPdfPath();
            File.WriteAllText(wordPath, "placeholder");
            File.SetLastWriteTimeUtc(wordPath, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            wordToPdf
                .SetupSequence(w => w.ConvertToPdfAsync(wordPath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(firstPdfPath)
                .ReturnsAsync(secondPdfPath);

            try
            {
                string first = await service.GetPdfPathAsync(wordPath, CancellationToken.None);

                File.SetLastWriteTimeUtc(wordPath, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
                string second = await service.GetPdfPathAsync(wordPath, CancellationToken.None);

                Assert.Equal(firstPdfPath, first);
                Assert.Equal(secondPdfPath, second);
                Assert.False(File.Exists(firstPdfPath));
                wordToPdf.Verify(w => w.ConvertToPdfAsync(wordPath, It.IsAny<CancellationToken>()), Times.Exactly(2));
            }
            finally
            {
                File.Delete(wordPath);
                File.Delete(firstPdfPath);
                File.Delete(secondPdfPath);
            }
        }

        /// <summary>
        /// Word ファイルのサイズが変わった場合は更新日時が同じでも再変換することを検証する
        /// </summary>
        [Fact]
        public async Task GetPdfPathAsync_WordInputSizeChanged_Reconverts()
        {
            var wordToPdf = new Mock<IWordToPdfConversionService>();
            var service = CreateService(wordToPdf);
            string wordPath = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.docx");
            string firstPdfPath = CreateTempPdfPath();
            string secondPdfPath = CreateTempPdfPath();
            var lastWriteUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.WriteAllText(wordPath, "placeholder");
            File.SetLastWriteTimeUtc(wordPath, lastWriteUtc);

            wordToPdf
                .SetupSequence(w => w.ConvertToPdfAsync(wordPath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(firstPdfPath)
                .ReturnsAsync(secondPdfPath);

            try
            {
                await service.GetPdfPathAsync(wordPath, CancellationToken.None);

                File.WriteAllText(wordPath, "placeholder-modified");
                File.SetLastWriteTimeUtc(wordPath, lastWriteUtc);
                string second = await service.GetPdfPathAsync(wordPath, CancellationToken.None);

                Assert.Equal(secondPdfPath, second);
                wordToPdf.Verify(w => w.ConvertToPdfAsync(wordPath, It.IsAny<CancellationToken>()), Times.Exactly(2));
            }
            finally
            {
                File.Delete(wordPath);
                File.Delete(firstPdfPath);
                File.Delete(secondPdfPath);
            }
        }

        /// <summary>
        /// キャッシュ済みの一時 PDF が失われた場合は再変換することを検証する
        /// </summary>
        [Fact]
        public async Task GetPdfPathAsync_CachedPdfMissing_Reconverts()
        {
            var wordToPdf = new Mock<IWordToPdfConversionService>();
            var service = CreateService(wordToPdf);
            string wordPath = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.docx");
            string firstPdfPath = CreateTempPdfPath();
            string secondPdfPath = CreateTempPdfPath();
            File.WriteAllText(wordPath, "placeholder");

            wordToPdf
                .SetupSequence(w => w.ConvertToPdfAsync(wordPath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(firstPdfPath)
                .ReturnsAsync(secondPdfPath);

            try
            {
                await service.GetPdfPathAsync(wordPath, CancellationToken.None);

                File.Delete(firstPdfPath);
                string second = await service.GetPdfPathAsync(wordPath, CancellationToken.None);

                Assert.Equal(secondPdfPath, second);
                wordToPdf.Verify(w => w.ConvertToPdfAsync(wordPath, It.IsAny<CancellationToken>()), Times.Exactly(2));
            }
            finally
            {
                File.Delete(wordPath);
                File.Delete(firstPdfPath);
                File.Delete(secondPdfPath);
            }
        }

        /// <summary>
        /// 変換中に設定が変わった場合は古い設定の PDF をキャッシュせず、破棄して変換し直すことを検証する
        /// </summary>
        [Fact]
        public async Task GetPdfPathAsync_SettingsChangedDuringConversion_DiscardsStalePdfAndReconverts()
        {
            var wordToPdf = new Mock<IWordToPdfConversionService>();
            var settings = WordToPdfConversionSettingsTestHelper.Create();
            var service = CreateService(wordToPdf, settings);
            string wordPath = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.docx");
            string stalePdfPath = CreateTempPdfPath();
            string freshPdfPath = CreateTempPdfPath();
            File.WriteAllText(wordPath, "placeholder");

            var firstConversion = new TaskCompletionSource<string>();
            wordToPdf
                .SetupSequence(w => w.ConvertToPdfAsync(wordPath, It.IsAny<CancellationToken>()))
                .Returns(firstConversion.Task)
                .ReturnsAsync(freshPdfPath);

            try
            {
                Task<string> pending = service.GetPdfPathAsync(wordPath, CancellationToken.None);

                settings.Raise(s => s.SettingsChanged += null, EventArgs.Empty);
                firstConversion.SetResult(stalePdfPath);
                string result = await pending;

                Assert.Equal(freshPdfPath, result);
                Assert.False(File.Exists(stalePdfPath));
                wordToPdf.Verify(w => w.ConvertToPdfAsync(wordPath, It.IsAny<CancellationToken>()), Times.Exactly(2));

                string cached = await service.GetPdfPathAsync(wordPath, CancellationToken.None);
                Assert.Equal(freshPdfPath, cached);
                wordToPdf.Verify(w => w.ConvertToPdfAsync(wordPath, It.IsAny<CancellationToken>()), Times.Exactly(2));
            }
            finally
            {
                File.Delete(wordPath);
                File.Delete(stalePdfPath);
                File.Delete(freshPdfPath);
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
