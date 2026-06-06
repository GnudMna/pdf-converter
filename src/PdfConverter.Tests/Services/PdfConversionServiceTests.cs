using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using PdfConverter.Models;
using PdfConverter.Services;
using PdfConverter.Tests.Helpers;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="PdfConversionService"/> のページ数取得・変換・保存・検証・キャンセルを検証する
    /// </summary>
    public class PdfConversionServiceTests
    {
        static PdfConversionServiceTests()
        {
            _ = typeof(NativeDependencyBootstrap);
        }

        private readonly PdfConversionService _service = new PdfConversionService();

        /// <summary>
        /// 有効な PDF から正しいページ数が取得できることを検証する
        /// </summary>
        [Fact]
        public async Task GetPdfPageCountAsync_ValidPdf_ReturnsPageCount()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdf(pageCount: 2);

            try
            {
                int pageCount = await _service.GetPdfPageCountAsync(pdfPath);

                pageCount.Should().Be(2);
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
            }
        }

        /// <summary>
        /// 存在しないファイルを指定した場合に FileNotFoundException がスローされることを検証する
        /// </summary>
        [Fact]
        public async Task GetPdfPageCountAsync_MissingFile_ThrowsFileNotFoundException()
        {
            string missingPath = Path.Combine(Path.GetTempPath(), $"pdf-converter-missing-{Guid.NewGuid():N}.pdf");

            Func<Task> act = () => _service.GetPdfPageCountAsync(missingPath);

            await act.Should().ThrowAsync<FileNotFoundException>();
        }

        /// <summary>
        /// PDF ヘッダーを持たないファイルで InvalidDataException がスローされることを検証する
        /// </summary>
        [Fact]
        public async Task GetPdfPageCountAsync_InvalidPdfHeader_ThrowsInvalidDataException()
        {
            string invalidPath = PdfTestAssetFactory.CreateTempInvalidPdfFile();

            try
            {
                Func<Task> act = () => _service.GetPdfPageCountAsync(invalidPath);

                await act.Should().ThrowAsync<InvalidDataException>();
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(invalidPath);
            }
        }

        /// <summary>
        /// キャンセル済みトークンでは OperationCanceledException がスローされることを検証する
        /// </summary>
        [Fact]
        public async Task GetPdfPageCountAsync_CancelledToken_ThrowsOperationCanceledException()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdf();

            try
            {
                using (var cts = new CancellationTokenSource())
                {
                    cts.Cancel();

                    Func<Task> act = () => _service.GetPdfPageCountAsync(pdfPath, cts.Token);

                    await act.Should().ThrowAsync<OperationCanceledException>();
                }
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
            }
        }

        /// <summary>
        /// 指定ページをビットマップへ変換できることを検証する
        /// </summary>
        [Fact]
        public async Task ConvertPdfPageToImageAsync_ValidPage_ReturnsBitmap()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdf();

            try
            {
                StaTestHelper.Run(async () =>
                {
                    var bitmap = await _service.ConvertPdfPageToImageAsync(pdfPath, pageIndex: 0);

                    bitmap.Should().NotBeNull();
                    bitmap.PixelWidth.Should().BeGreaterThan(0);
                    bitmap.PixelHeight.Should().BeGreaterThan(0);
                    bitmap.IsFrozen.Should().BeTrue();
                });
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
            }
        }

        /// <summary>
        /// 幅指定モードで出力サイズがスケーリングされることを検証する
        /// </summary>
        [Fact]
        public async Task ConvertPdfPageToImageAsync_WidthMode_ScalesOutput()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdf();

            try
            {
                StaTestHelper.Run(async () =>
                {
                    var bitmap = await _service.ConvertPdfPageToImageAsync(
                        pdfPath,
                        pageIndex: 0,
                        ResolutionMode.Width,
                        value: 100);

                    bitmap.PixelWidth.Should().Be(100);
                    bitmap.PixelHeight.Should().Be(100);
                });
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
            }
        }

        /// <summary>
        /// 範囲外のページインデックスで ArgumentOutOfRangeException がスローされることを検証する
        /// </summary>
        [Fact]
        public async Task ConvertPdfPageToImageAsync_OutOfRangePage_ThrowsArgumentOutOfRangeException()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdf();

            try
            {
                Func<Task> act = () => _service.ConvertPdfPageToImageAsync(pdfPath, pageIndex: 99);

                await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
            }
        }

        /// <summary>
        /// 指定ページを画像ファイルとして保存し、進捗が報告されることを検証する
        /// </summary>
        [Fact]
        public async Task SavePdfPagesToImagesAsync_SelectedPages_WritesFilesAndReportsProgress()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdf(pageCount: 2);
            string outputDirectory = PdfTestAssetFactory.CreateTempOutputDirectory();
            var progressReports = new List<SaveProgressReport>();

            try
            {
                await _service.SavePdfPagesToImagesAsync(
                    pdfPath,
                    new[] { 0, 1 },
                    outputDirectory,
                    saveAllPages: false,
                    format: OutputImageFormat.Png,
                    progress: new Progress<SaveProgressReport>(progressReports.Add));

                File.Exists(Path.Combine(outputDirectory, "page_1.png")).Should().BeTrue();
                File.Exists(Path.Combine(outputDirectory, "page_2.png")).Should().BeTrue();
                progressReports.Should().NotBeEmpty();
                progressReports.Last().Percentage.Should().Be(100);
                progressReports.Last().Message.Should().Contain("2/2");
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
                PdfTestAssetFactory.DeleteDirectoryIfExists(outputDirectory);
            }
        }

        /// <summary>
        /// 全ページ保存モードで全ページ分のファイルが出力されることを検証する
        /// </summary>
        [Fact]
        public async Task SavePdfPagesToImagesAsync_AllPages_WritesAllPageFiles()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdf(pageCount: 3);
            string outputDirectory = PdfTestAssetFactory.CreateTempOutputDirectory();

            try
            {
                await _service.SavePdfPagesToImagesAsync(
                    pdfPath,
                    pageIndexes: null,
                    outputDirectory,
                    saveAllPages: true,
                    format: OutputImageFormat.Jpeg);

                Directory.GetFiles(outputDirectory, "page_*.jpg").Should().HaveCount(3);
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
                PdfTestAssetFactory.DeleteDirectoryIfExists(outputDirectory);
            }
        }

        /// <summary>
        /// 保存対象ページが空の場合に ArgumentException がスローされることを検証する
        /// </summary>
        [Fact]
        public async Task SavePdfPagesToImagesAsync_EmptyPageSelection_ThrowsArgumentException()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdf();
            string outputDirectory = PdfTestAssetFactory.CreateTempOutputDirectory();

            try
            {
                Func<Task> act = () => _service.SavePdfPagesToImagesAsync(
                    pdfPath,
                    Enumerable.Empty<int>(),
                    outputDirectory,
                    saveAllPages: false);

                await act.Should().ThrowAsync<ArgumentException>();
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
                PdfTestAssetFactory.DeleteDirectoryIfExists(outputDirectory);
            }
        }

        /// <summary>
        /// 小さい PDF では CPU コア数と上限 4 の小さい方で並列度が決まることを検証する
        /// </summary>
        [Theory]
        [InlineData(8, 1 * 1024 * 1024, 4)]
        [InlineData(2, 1 * 1024 * 1024, 2)]
        [InlineData(1, 1 * 1024 * 1024, 1)]
        public void ResolveSaveParallelism_SmallPdf_UsesCpuBoundCap(int processorCount, long fileSizeBytes, int expected)
        {
            PdfConversionService.ResolveSaveParallelism(processorCount, fileSizeBytes).Should().Be(expected);
        }

        /// <summary>
        /// 中サイズ PDF では並列度が最大 2 に制限されることを検証する
        /// </summary>
        [Theory]
        [InlineData(8, 16L * 1024 * 1024, 2)]
        [InlineData(2, 16L * 1024 * 1024, 2)]
        public void ResolveSaveParallelism_MediumPdf_CapsAtTwo(int processorCount, long fileSizeBytes, int expected)
        {
            PdfConversionService.ResolveSaveParallelism(processorCount, fileSizeBytes).Should().Be(expected);
        }

        /// <summary>
        /// 大きい PDF では並列度が 1 に制限されることを検証する
        /// </summary>
        [Theory]
        [InlineData(8, 48L * 1024 * 1024, 1)]
        [InlineData(8, 100L * 1024 * 1024, 1)]
        public void ResolveSaveParallelism_LargePdf_IsSequential(int processorCount, long fileSizeBytes, int expected)
        {
            PdfConversionService.ResolveSaveParallelism(processorCount, fileSizeBytes).Should().Be(expected);
        }

        /// <summary>
        /// キャンセル済みトークンでは保存処理が早期終了し、出力ファイルを作成しないことを検証する
        /// </summary>
        [Fact]
        public async Task SavePdfPagesToImagesAsync_CancelledToken_DoesNotWriteFiles()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdf(pageCount: 2);
            string outputDirectory = PdfTestAssetFactory.CreateTempOutputDirectory();

            try
            {
                using (var cts = new CancellationTokenSource())
                {
                    cts.Cancel();

                    await _service.SavePdfPagesToImagesAsync(
                        pdfPath,
                        new[] { 0, 1 },
                        outputDirectory,
                        saveAllPages: false,
                        cancellationToken: cts.Token);
                }

                Directory.GetFiles(outputDirectory).Should().BeEmpty();
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
                PdfTestAssetFactory.DeleteDirectoryIfExists(outputDirectory);
            }
        }
    }
}
