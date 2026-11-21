using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using PdfConverter.Infrastructure;
using PdfConverter.Models;
using PdfConverter.Services;
using PdfConverter.Tests.Helpers;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="BatchExportService"/> のバッチ変換処理を検証する
    /// </summary>
    public class BatchExportServiceTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// ヘルプ表示時は成功終了コードを返すことを検証する
        /// </summary>
        [Fact]
        public async Task RunAsync_ShowHelp_ReturnsSuccess()
        {
            var service = CreateService();

            int exitCode = await service.RunAsync(new BatchCliOptions { ShowHelp = true }, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(BatchCliParser.SuccessExitCode, exitCode);
        }

        /// <summary>
        /// 画像出力時に PDF 変換サービスへ期待どおりの引数が渡されることを検証する
        /// </summary>
        [Fact]
        public async Task RunAsync_ImageExport_CallsPdfConversionService()
        {
            string outputDir = CreateTempDirectory();
            string pdfPath = CreateTempPdfPath();
            var pdfService = new Mock<IPdfConversionService>();
            pdfService
                .Setup(s => s.GetPdfPageCountAsync(pdfPath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);
            pdfService
                .Setup(s => s.SavePdfPagesToImagesAsync(
                    pdfPath,
                    It.IsAny<IEnumerable<int>>(),
                    outputDir,
                    true,
                    ResolutionMode.Width,
                    1080,
                    OutputImageFormat.Png,
                    false,
                    It.IsAny<IProgress<SaveProgressReport>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var documentService = new Mock<IDocumentPdfSourceService>();
            documentService
                .Setup(s => s.GetPdfPathAsync(pdfPath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(pdfPath);

            var service = CreateService(pdfService.Object, documentService.Object);
            var options = new BatchCliOptions
            {
                OutputPath = outputDir,
                Format = OutputImageFormat.Png,
                ResolutionMode = ResolutionMode.Width,
                ResolutionValue = "1080",
                PreserveTransparency = false,
            };
            options.InputFiles.Add(pdfPath);

            int exitCode = await service.RunAsync(options, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(BatchCliParser.SuccessExitCode, exitCode);
            pdfService.VerifyAll();
            documentService.VerifyAll();
        }

        /// <summary>
        /// 既存ファイルがあり上書き指定がない場合は失敗終了コードを返すことを検証する
        /// </summary>
        [Fact]
        public async Task RunAsync_ExistingOutputWithoutOverwrite_ReturnsFailure()
        {
            string outputDir = CreateTempDirectory();
            string pdfPath = CreateTempPdfPath();
            string existingFile = Path.Combine(outputDir, "page_1.png");
            File.WriteAllText(existingFile, "existing");

            var pdfService = new Mock<IPdfConversionService>();
            pdfService
                .Setup(s => s.GetPdfPageCountAsync(pdfPath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            var documentService = new Mock<IDocumentPdfSourceService>();
            documentService
                .Setup(s => s.GetPdfPathAsync(pdfPath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(pdfPath);

            var service = CreateService(pdfService.Object, documentService.Object);
            var options = new BatchCliOptions
            {
                OutputPath = outputDir,
                Format = OutputImageFormat.Png,
                ResolutionMode = ResolutionMode.Default,
            };
            options.InputFiles.Add(pdfPath);

            int exitCode = await service.RunAsync(options, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(BatchCliParser.RuntimeErrorExitCode, exitCode);
            pdfService.Verify(
                s => s.SavePdfPagesToImagesAsync(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<int>>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<ResolutionMode>(),
                    It.IsAny<double>(),
                    It.IsAny<OutputImageFormat>(),
                    It.IsAny<bool>(),
                    It.IsAny<IProgress<SaveProgressReport>>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        /// <summary>
        /// Word の PDF 出力時に変換結果をコピーすることを検証する
        /// </summary>
        [Fact]
        public async Task RunAsync_ExportPdf_CopiesGeneratedPdf()
        {
            string outputDir = CreateTempDirectory();
            string wordPath = CreateTempWordPath();
            string generatedPdf = CreateTempPdfPath();
            File.WriteAllText(generatedPdf, "pdf");

            var documentService = new Mock<IDocumentPdfSourceService>();
            documentService
                .Setup(s => s.GetPdfPathAsync(wordPath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(generatedPdf);

            var service = CreateService(documentPdfSourceService: documentService.Object);
            var options = new BatchCliOptions
            {
                OutputPath = outputDir,
                ExportPdfOnly = true,
                OverwriteExisting = true,
            };
            options.InputFiles.Add(wordPath);

            int exitCode = await service.RunAsync(options, cancellationToken: TestContext.Current.CancellationToken);

            string destinationPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(wordPath) + ".pdf");
            Assert.Equal(BatchCliParser.SuccessExitCode, exitCode);
            Assert.True(File.Exists(destinationPath));
            Assert.Equal("pdf", File.ReadAllText(destinationPath));
        }

        /// <summary>
        /// 別フォルダーの同名 Word 文書を PDF 出力しても、互いを上書きせず連番を付けることを検証する
        /// </summary>
        [Fact]
        public async Task RunAsync_ExportPdfWithSameFileNames_AssignsNumberedDestinations()
        {
            string outputDir = CreateTempDirectory();
            string wordA = CreateNamedTempFile("a", "x.docx");
            string wordB = CreateNamedTempFile("b", "x.docx");
            string pdfA = CreateTempPdfPath();
            string pdfB = CreateTempPdfPath();
            File.WriteAllText(pdfA, "pdf-a");
            File.WriteAllText(pdfB, "pdf-b");

            var documentService = new Mock<IDocumentPdfSourceService>();
            documentService
                .Setup(s => s.GetPdfPathAsync(wordA, It.IsAny<CancellationToken>()))
                .ReturnsAsync(pdfA);
            documentService
                .Setup(s => s.GetPdfPathAsync(wordB, It.IsAny<CancellationToken>()))
                .ReturnsAsync(pdfB);

            var service = CreateService(documentPdfSourceService: documentService.Object);
            var options = new BatchCliOptions
            {
                OutputPath = outputDir,
                ExportPdfOnly = true,
                OverwriteExisting = true,
                Quiet = true,
            };
            options.InputFiles.Add(wordA);
            options.InputFiles.Add(wordB);

            int exitCode = await service.RunAsync(options, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(BatchCliParser.SuccessExitCode, exitCode);
            Assert.Equal("pdf-a", File.ReadAllText(Path.Combine(outputDir, "x.pdf")));
            Assert.Equal("pdf-b", File.ReadAllText(Path.Combine(outputDir, "x_2.pdf")));
        }

        /// <summary>
        /// 別フォルダーの同名ファイルを画像出力しても、出力サブフォルダーが重ならず連番が付くことを検証する
        /// </summary>
        [Fact]
        public async Task RunAsync_ImageExportWithSameFileNames_AssignsNumberedFolders()
        {
            string outputDir = CreateTempDirectory();
            string pdfA = CreateNamedTempFile("a", "x.pdf");
            string pdfB = CreateNamedTempFile("b", "x.pdf");
            var savedFolders = new List<string>();

            var pdfService = new Mock<IPdfConversionService>();
            pdfService
                .Setup(s => s.GetPdfPageCountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);
            pdfService
                .Setup(s => s.SavePdfPagesToImagesAsync(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<int>>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<ResolutionMode>(),
                    It.IsAny<double>(),
                    It.IsAny<OutputImageFormat>(),
                    It.IsAny<bool>(),
                    It.IsAny<IProgress<SaveProgressReport>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, IEnumerable<int>, string, bool, ResolutionMode, double, OutputImageFormat, bool, IProgress<SaveProgressReport>, CancellationToken>(
                    (_, _, folder, _, _, _, _, _, _, _) => savedFolders.Add(folder))
                .Returns(Task.CompletedTask);

            var documentService = new Mock<IDocumentPdfSourceService>();
            documentService
                .Setup(s => s.GetPdfPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string path, CancellationToken _) => path);

            var service = CreateService(pdfService.Object, documentService.Object);
            var options = new BatchCliOptions
            {
                OutputPath = outputDir,
                Format = OutputImageFormat.Png,
                ResolutionMode = ResolutionMode.Default,
                OverwriteExisting = true,
                Quiet = true,
            };
            options.InputFiles.Add(pdfA);
            options.InputFiles.Add(pdfB);

            int exitCode = await service.RunAsync(options, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(BatchCliParser.SuccessExitCode, exitCode);
            Assert.Equal(
                new[] { Path.Combine(outputDir, "x"), Path.Combine(outputDir, "x_2") },
                savedFolders);
        }

        /// <summary>
        /// 継続を処理しない SynchronizationContext (ブロック中の UI スレッド相当) 上で
        /// 同期的に完了を待ってもデッドロックしないことを検証する
        /// </summary>
        [Fact]
        public void RunAsync_BlockingWaitOnNonPumpingContext_DoesNotDeadlock()
        {
            string outputDir = CreateTempDirectory();
            string pdfPath = CreateTempPdfPath();
            var pdfService = new Mock<IPdfConversionService>();
            pdfService
                .Setup(s => s.GetPdfPageCountAsync(pdfPath, It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    await Task.Delay(50);
                    return 1;
                });
            pdfService
                .Setup(s => s.SavePdfPagesToImagesAsync(
                    pdfPath,
                    It.IsAny<IEnumerable<int>>(),
                    outputDir,
                    true,
                    It.IsAny<ResolutionMode>(),
                    It.IsAny<double>(),
                    It.IsAny<OutputImageFormat>(),
                    It.IsAny<bool>(),
                    It.IsAny<IProgress<SaveProgressReport>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(() => Task.Delay(50));

            var documentService = new Mock<IDocumentPdfSourceService>();
            documentService
                .Setup(s => s.GetPdfPathAsync(pdfPath, It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    await Task.Delay(50);
                    return pdfPath;
                });

            var service = CreateService(pdfService.Object, documentService.Object);
            var options = new BatchCliOptions
            {
                OutputPath = outputDir,
                Format = OutputImageFormat.Png,
                ResolutionMode = ResolutionMode.Default,
                Quiet = true,
            };
            options.InputFiles.Add(pdfPath);

            int? exitCode = null;
            Exception failure = null;
            var thread = new Thread(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new NonPumpingSynchronizationContext());
                try
                {
                    exitCode = service.RunAsync(options).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            })
            {
                IsBackground = true,
            };

            thread.Start();
            bool completed = thread.Join(TimeSpan.FromSeconds(10));

            Assert.True(completed, "RunAsync の同期待機がデッドロックした");
            Assert.Null(failure);
            Assert.Equal(BatchCliParser.SuccessExitCode, exitCode);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        private static BatchExportService CreateService(
            IPdfConversionService pdfConversionService = null,
            IDocumentPdfSourceService documentPdfSourceService = null,
            IImageExportSettings imageExportSettings = null)
        {
            return new BatchExportService(
                pdfConversionService ?? new Mock<IPdfConversionService>().Object,
                documentPdfSourceService ?? new Mock<IDocumentPdfSourceService>().Object,
                imageExportSettings ?? ImageExportSettingsTestHelper.Create().Object);
        }

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-batch-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return path;
        }

        private static string CreateTempPdfPath()
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-batch-{Guid.NewGuid():N}.pdf");
            File.WriteAllBytes(path, new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D });
            return path;
        }

        private static string CreateNamedTempFile(string subFolder, string fileName)
        {
            string directory = Path.Combine(CreateTempDirectory(), subFolder);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, fileName);
            File.WriteAllText(path, "content");
            return path;
        }

        private static string CreateTempWordPath()
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-batch-{Guid.NewGuid():N}.docx");
            File.WriteAllText(path, "word");
            return path;
        }


        /********************************************************************************/
        /*                                   ネスト型                                   */
        /********************************************************************************/
        /// <summary>
        /// 投稿された継続を一切実行しない SynchronizationContext<br/>
        /// メッセージループが同期待機でブロックされた UI スレッドを模擬する
        /// </summary>
        private sealed class NonPumpingSynchronizationContext : SynchronizationContext
        {
            /// <inheritdoc/>
            public override void Post(SendOrPostCallback d, object state)
            {
            }

            /// <inheritdoc/>
            public override void Send(SendOrPostCallback d, object state)
            {
            }
        }
    }
}
