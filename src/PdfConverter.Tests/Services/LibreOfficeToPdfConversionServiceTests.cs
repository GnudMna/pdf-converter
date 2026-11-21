using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public sealed class LibreOfficeToPdfConversionServiceTests : IDisposable
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 変換で生成された PDF・作業ディレクトリ・変換専用プロファイルを置くインスタンスディレクトリを削除する
        /// </summary>
        public void Dispose()
        {
            PdfConverterTempDirectory.CleanupInstanceDirectory();
        }

        /// <summary>
        /// 起動引数の先頭で変換専用プロファイルを指定し、headless 変換に必要なオプションが続くことを検証する
        /// </summary>
        [Fact]
        public void BuildArguments_SpecifiesProfileAndHeadlessConvertOptions()
        {
            var settings = WordToPdfConversionSettingsTestHelper.Create();

            IReadOnlyList<string> arguments = LibreOfficeToPdfConversionService.BuildArguments(
                @"C:\temp\profile",
                @"C:\output",
                @"C:\docs\sample.docx",
                settings.Object);

            Assert.Equal("-env:UserInstallation=file:///C:/temp/profile", arguments[0]);
            Assert.Contains("--headless", arguments);
            Assert.Contains("--norestore", arguments);
            int convertToIndex = arguments.ToList().IndexOf("--convert-to");
            Assert.Equal(LibreOfficePdfExportFilterBuilder.BuildConvertToArgument(settings.Object), arguments[convertToIndex + 1]);
            int outDirIndex = arguments.ToList().IndexOf("--outdir");
            Assert.Equal(@"C:\output", arguments[outDirIndex + 1]);
            Assert.Equal(@"C:\docs\sample.docx", arguments[^1]);
        }

        /// <summary>
        /// プロファイルのパスに含まれる空白・日本語・URL の予約文字が、ファイル URL としてエスケープされることを検証する
        /// </summary>
        [Fact]
        public void BuildArguments_ProfilePathWithSpecialCharacters_IsEscapedAsFileUrl()
        {
            var settings = WordToPdfConversionSettingsTestHelper.Create();

            IReadOnlyList<string> arguments = LibreOfficeToPdfConversionService.BuildArguments(
                @"C:\Users\テスト ユーザー\a#b%c\profile",
                @"C:\output",
                @"C:\docs\sample.docx",
                settings.Object);

            Assert.Equal(
                "-env:UserInstallation=file:///C:/Users/%E3%83%86%E3%82%B9%E3%83%88%20%E3%83%A6%E3%83%BC%E3%82%B6%E3%83%BC/a%23b%25c/profile",
                arguments[0]);
        }

        /// <summary>
        /// 空白・引用符・末尾の円記号を含むパスも、引用符で囲まずそのままの値で引数になることを検証する
        /// </summary>
        [Fact]
        public void BuildArguments_PathsWithSpecialCharacters_AreKeptAsIs()
        {
            var settings = WordToPdfConversionSettingsTestHelper.Create();

            IReadOnlyList<string> arguments = LibreOfficeToPdfConversionService.BuildArguments(
                @"C:\temp\profile",
                @"C:\out dir\",
                @"C:\path ""quoted""\file.docx",
                settings.Object);

            Assert.Contains(@"C:\out dir\", arguments);
            Assert.Equal(@"C:\path ""quoted""\file.docx", arguments[^1]);
        }

        /// <summary>
        /// 連続した変換で同じ変換専用プロファイルを再利用し、失敗した変換の後は別のプロファイルに作り直すことを検証する
        /// </summary>
        [Fact]
        public async Task ConvertToPdfAsync_ReusesProfileAndRecreatesAfterFailure()
        {
            string recordPath = Path.Combine(Path.GetTempPath(), $"pdf-converter-profile-record-{Guid.NewGuid():N}.txt");
            string successSoffice = FakeSofficeHelper.CreateProfileRecordingWrapper(recordPath, success: true);
            string failingSoffice = FakeSofficeHelper.CreateProfileRecordingWrapper(recordPath, success: false);
            var successService = new LibreOfficeToPdfConversionService(CreateSettings(successSoffice).Object);
            var failingService = new LibreOfficeToPdfConversionService(CreateSettings(failingSoffice).Object);
            string wordPath = CreateTempWordFile();

            try
            {
                await successService.ConvertToPdfAsync(wordPath, TestContext.Current.CancellationToken);
                await successService.ConvertToPdfAsync(wordPath, TestContext.Current.CancellationToken);
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => failingService.ConvertToPdfAsync(wordPath, TestContext.Current.CancellationToken));
                await successService.ConvertToPdfAsync(wordPath, TestContext.Current.CancellationToken);

                string[] profiles = File.ReadAllLines(recordPath);
                Assert.Equal(4, profiles.Length);
                Assert.All(profiles, profile => Assert.StartsWith("-env:UserInstallation=file:///", profile));
                Assert.Contains(LibreOfficeToPdfConversionService.ProfileDirectoryPrefix, profiles[0]);
                Assert.Equal(profiles[0], profiles[1]);
                Assert.Equal(profiles[0], profiles[2]);
                Assert.NotEqual(profiles[2], profiles[3]);
            }
            finally
            {
                File.Delete(wordPath);
                FakeSofficeHelper.DeleteWrapper(successSoffice);
                FakeSofficeHelper.DeleteWrapper(failingSoffice);
                File.Delete(recordPath);
            }
        }

        /// <summary>
        /// 変換専用プロファイルがこのプロセスのインスタンスディレクトリ配下に置かれることを検証する
        /// </summary>
        [Fact]
        public async Task ConvertToPdfAsync_ProfileIsUnderInstanceDirectory()
        {
            string recordPath = Path.Combine(Path.GetTempPath(), $"pdf-converter-profile-record-{Guid.NewGuid():N}.txt");
            string fakeSoffice = FakeSofficeHelper.CreateProfileRecordingWrapper(recordPath, success: true);
            var service = new LibreOfficeToPdfConversionService(CreateSettings(fakeSoffice).Object);
            string wordPath = CreateTempWordFile();

            try
            {
                await service.ConvertToPdfAsync(wordPath, TestContext.Current.CancellationToken);

                string profileUrl = File.ReadAllLines(recordPath)[0].Substring("-env:UserInstallation=".Length);
                string profilePath = new Uri(profileUrl).LocalPath;
                Assert.Equal(
                    PdfConverterTempDirectory.GetInstancePath(),
                    Path.GetDirectoryName(profilePath),
                    StringComparer.OrdinalIgnoreCase);
            }
            finally
            {
                File.Delete(wordPath);
                FakeSofficeHelper.DeleteWrapper(fakeSoffice);
                File.Delete(recordPath);
            }
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
                string pdfPath = await service.ConvertToPdfAsync(wordPath, cancellationToken: TestContext.Current.CancellationToken);

                Assert.False(string.IsNullOrWhiteSpace(pdfPath));
                Assert.True(File.Exists(pdfPath));
                Assert.Contains("%PDF", File.ReadAllText(pdfPath));
            }
            finally
            {
                File.Delete(wordPath);
                FakeSofficeHelper.DeleteWrapper(fakeSoffice);
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
                FakeSofficeHelper.DeleteWrapper(fakeSoffice);
            }
        }

        /// <summary>
        /// 大量の標準出力 / 標準エラー出力でもデッドロックせず PDF を生成できることを検証する
        /// </summary>
        [Fact]
        public async Task ConvertToPdfAsync_WithVerboseFakeSoffice_ReturnsGeneratedPdfPath()
        {
            string fakeSoffice = FakeSofficeHelper.CreateSuccessfulVerboseWrapper();
            var settings = CreateSettings(fakeSoffice);
            var service = new LibreOfficeToPdfConversionService(settings.Object);
            string wordPath = CreateTempWordFile();

            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                {
                    string pdfPath = await service.ConvertToPdfAsync(wordPath, cts.Token);

                    Assert.False(string.IsNullOrWhiteSpace(pdfPath));
                    Assert.True(File.Exists(pdfPath));
                    Assert.Contains("%PDF", File.ReadAllText(pdfPath));
                }
            }
            finally
            {
                File.Delete(wordPath);
                FakeSofficeHelper.DeleteWrapper(fakeSoffice);
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
