using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
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

            arguments.Should().Contain("--headless");
            arguments.Should().Contain("--convert-to");
            arguments.Should().Contain("--outdir");
            arguments.Should().Contain(LibreOfficeToPdfConversionService.QuoteArgument(outputDirectory));
            arguments.Should().Contain(LibreOfficeToPdfConversionService.QuoteArgument(wordPath));
        }

        /// <summary>
        /// 引用符を含むパスがエスケープされることを検証する
        /// </summary>
        [Fact]
        public void QuoteArgument_EscapesEmbeddedQuotes()
        {
            LibreOfficeToPdfConversionService.QuoteArgument(@"C:\path ""quoted""\file.docx")
                .Should().Be(@"""C:\path \""quoted\""\file.docx""");
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

            await act.Should().ThrowAsync<FileNotFoundException>();
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
                    await act.Should().ThrowAsync<OperationCanceledException>();
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

                pdfPath.Should().NotBeNullOrWhiteSpace();
                File.Exists(pdfPath).Should().BeTrue();
                File.ReadAllText(pdfPath).Should().Contain("%PDF");
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

                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage($"*終了コード {FakeSofficeHelper.FailureExitCode}*");
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
