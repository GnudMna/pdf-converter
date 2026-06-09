using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using PdfConverter.Models;
using PdfConverter.Services;
using PdfConverter.Tests.Helpers;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="LibreOfficeToPdfConversionService"/> の検証・引数組み立て・偽プロセス連携を検証する
    /// </summary>
    public class LibreOfficeToPdfConversionServiceTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 起動引数に headless 変換に必要なオプションが含まれることを検証する
        /// </summary>
        [Fact]
        public void BuildArguments_IncludesHeadlessConvertOptions()
        {
            var settings = WordToPdfConversionSettingsTestHelper.Create();
            string outputDirectory = @"C:\output";
            string wordPath = @"C:\docs\sample.docx";

            string arguments = LibreOfficeToPdfConversionService.BuildArguments(outputDirectory, wordPath, settings.Object);

            Assert.Contains("--headless", arguments);
            Assert.Contains("--convert-to", arguments);
            Assert.Contains("--outdir", arguments);
            Assert.Contains(LibreOfficeToPdfConversionService.QuoteArgument(outputDirectory), arguments);
            Assert.Contains(LibreOfficeToPdfConversionService.QuoteArgument(wordPath), arguments);
        }

        /// <summary>
        /// 引用符を含むパスがエスケープされることを検証する
        /// </summary>
        [Fact]
        public void QuoteArgument_EscapesEmbeddedQuotes()
        {
            Assert.Equal(@"""C:\path \""quoted\""\file.docx""", LibreOfficeToPdfConversionService.QuoteArgument(@"C:\path ""quoted""\file.docx"));
        }

        /// <summary>
        /// 存在しない Word ファイルを指定した場合に FileNotFoundException がスローされることを検証する
        /// </summary>
        [Fact]
        public async Task ConvertToPdfAsync_MissingFile_ThrowsFileNotFoundException()
        {
            var service = CreateService();
            string missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.docx");

            Func<Task> act = () => service.ConvertToPdfAsync(missingPath);

            await Assert.ThrowsAsync<FileNotFoundException>(act);
        }

        /// <summary>
        /// キャンセル済みトークンでは OperationCanceledException がスローされることを検証する
        /// </summary>
        [Fact]
        public async Task ConvertToPdfAsync_CancelledToken_ThrowsOperationCanceledException()
        {
            var service = CreateService();
            string wordPath = CreateTempWordFile();

            try
            {
                using (var cts = new CancellationTokenSource())
                {
                    cts.Cancel();
                    Func<Task> act = () => service.ConvertToPdfAsync(wordPath, cts.Token);
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
                }
            }
            finally
            {
                File.Delete(wordPath);
            }
        }

        /// <summary>
        /// 偽 soffice ラッパー経由で PDF ファイルが生成されることを検証する
        /// </summary>
        [Fact]
        public async Task ConvertToPdfAsync_WithFakeSoffice_ReturnsGeneratedPdfPath()
        {
            string fakeSoffice = FakeSofficeHelper.CreateSuccessfulWrapper();
            var settings = CreateSettings(fakeSoffice);
            var service = new LibreOfficeToPdfConversionService(settings.Object);
            string wordPath = CreateTempWordFile();

            try
            {
                string pdfPath = await service.ConvertToPdfAsync(wordPath);

                Assert.False(string.IsNullOrWhiteSpace(pdfPath));
                Assert.True(File.Exists(pdfPath));
                Assert.Contains("%PDF", File.ReadAllText(pdfPath));
            }
            finally
            {
                File.Delete(wordPath);
                File.Delete(fakeSoffice);
            }
        }

        /// <summary>
        /// 偽 soffice が失敗終了コードを返した場合に InvalidOperationException がスローされることを検証する
        /// </summary>
        [Fact]
        public async Task ConvertToPdfAsync_FakeSofficeFailure_ThrowsInvalidOperationException()
        {
            string fakeSoffice = FakeSofficeHelper.CreateFailingWrapper();
            var settings = CreateSettings(fakeSoffice);
            var service = new LibreOfficeToPdfConversionService(settings.Object);
            string wordPath = CreateTempWordFile();

            try
            {
                Func<Task> act = () => service.ConvertToPdfAsync(wordPath);

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(act);
                Assert.Contains($"終了コード {FakeSofficeHelper.FailureExitCode}", ex.Message);
            }
            finally
            {
                File.Delete(wordPath);
                File.Delete(fakeSoffice);
            }
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/

        private static LibreOfficeToPdfConversionService CreateService()
        {
            return new LibreOfficeToPdfConversionService(WordToPdfConversionSettingsTestHelper.Create().Object);
        }

        private static Mock<IWordToPdfConversionSettings> CreateSettings(string libreOfficePath)
        {
            var settings = WordToPdfConversionSettingsTestHelper.Create();
            settings.SetupGet(s => s.LibreOfficePath).Returns(libreOfficePath);
            return settings;
        }

        private static string CreateTempWordFile()
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.docx");
            File.WriteAllText(path, "placeholder");
            return path;
        }
    }
}
