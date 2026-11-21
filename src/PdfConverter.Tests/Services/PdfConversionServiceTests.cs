using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using PdfConverter.Models;
using PdfConverter.Services;
using PdfConverter.Tests.Helpers;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="PdfConversionService"/> のページ数取得・変換・保存・検証・キャンセルを検証する
    /// </summary>
    public sealed class PdfConversionServiceTests : IDisposable
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>メモリキャッシュの上限 (64 MB) を超える PDF を作るためのコメント行のバイト数</summary>
        private const long LargePdfPaddingBytes = 65L * 1024 * 1024;

        private readonly PdfConversionService _service = new PdfConversionService();


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// テストごとに生成したサービスを破棄する
        /// </summary>
        public void Dispose()
        {
            _service.Dispose();
        }

        /// <summary>
        /// 有効な PDF から正しいページ数が取得できることを検証する
        /// </summary>
        [Fact]
        public async Task GetPdfPageCountAsync_ValidPdf_ReturnsPageCount()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdf(pageCount: 2);

            try
            {
                int pageCount = await _service.GetPdfPageCountAsync(pdfPath, cancellationToken: TestContext.Current.CancellationToken);

                Assert.Equal(2, pageCount);
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

            await Assert.ThrowsAsync<FileNotFoundException>(act);
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

                await Assert.ThrowsAsync<InvalidDataException>(act);
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(invalidPath);
            }
        }

        /// <summary>
        /// PDF ヘッダーの前に先頭 1024 バイト以内のデータがある PDF を読み込めることを検証する
        /// </summary>
        [Fact]
        public async Task GetPdfPageCountAsync_HeaderWithinFirst1024Bytes_ReturnsPageCount()
        {
            byte[] leadingBytes = Enumerable.Repeat((byte)' ', 512).ToArray();
            string pdfPath = PdfTestAssetFactory.CreateTempPdfWithLeadingBytes(leadingBytes, pageCount: 2);

            try
            {
                int pageCount = await _service.GetPdfPageCountAsync(pdfPath, cancellationToken: TestContext.Current.CancellationToken);

                Assert.Equal(2, pageCount);
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
            }
        }

        /// <summary>
        /// PDF ヘッダーが先頭 1024 バイトより後ろにある場合に InvalidDataException がスローされることを検証する
        /// </summary>
        [Fact]
        public async Task GetPdfPageCountAsync_HeaderBeyondFirst1024Bytes_ThrowsInvalidDataException()
        {
            byte[] leadingBytes = Enumerable.Repeat((byte)' ', 1025).ToArray();
            string pdfPath = PdfTestAssetFactory.CreateTempPdfWithLeadingBytes(leadingBytes);

            try
            {
                Func<Task> act = () => _service.GetPdfPageCountAsync(pdfPath);

                await Assert.ThrowsAsync<InvalidDataException>(act);
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
            }
        }

        /// <summary>
        /// PDF ヘッダーの探索位置の境界・不正入力を検証する
        /// </summary>
        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        [InlineData(1024, 1024)]
        [InlineData(1025, -1)]
        public void FindPdfHeaderOffset_LeadingBytes_ReturnsExpectedOffset(int leadingLength, int expected)
        {
            byte[] bytes = Enumerable.Repeat((byte)0, leadingLength)
                .Concat(new[] { (byte)'%', (byte)'P', (byte)'D', (byte)'F', (byte)'-' })
                .ToArray();

            Assert.Equal(expected, PdfConversionService.FindPdfHeaderOffset(bytes));
        }

        /// <summary>
        /// null・4 バイト未満・ヘッダーなしの入力で -1 を返すことを検証する
        /// </summary>
        [Fact]
        public void FindPdfHeaderOffset_InvalidInput_ReturnsMinusOne()
        {
            Assert.Equal(-1, PdfConversionService.FindPdfHeaderOffset(null));
            Assert.Equal(-1, PdfConversionService.FindPdfHeaderOffset(new byte[] { (byte)'%', (byte)'P', (byte)'D' }));
            Assert.Equal(-1, PdfConversionService.FindPdfHeaderOffset(System.Text.Encoding.ASCII.GetBytes("NOT A PDF FILE")));
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

                    await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
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

                    Assert.NotNull(bitmap);
                    Assert.True(bitmap.PixelWidth > 0);
                    Assert.True(bitmap.PixelHeight > 0);
                    Assert.True(bitmap.IsFrozen);
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

                    Assert.Equal(100, bitmap.PixelWidth);
                    Assert.Equal(100, bitmap.PixelHeight);
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

                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(act);
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
                    progress: new SynchronousProgress<SaveProgressReport>(progressReports.Add),
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(File.Exists(Path.Combine(outputDirectory, "page_1.png")));
                Assert.True(File.Exists(Path.Combine(outputDirectory, "page_2.png")));
                Assert.NotEmpty(progressReports);
                Assert.Equal(100, progressReports.Last().Percentage);
                Assert.Contains("2/2", progressReports.Last().Message);
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
                PdfTestAssetFactory.DeleteDirectoryIfExists(outputDirectory);
            }
        }

        /// <summary>
        /// 幅指定モードでページサイズが混在する場合も、各ページが指定幅で保存されることを検証する
        /// </summary>
        [Fact]
        public async Task SavePdfPagesToImagesAsync_WidthModeMixedPageSizes_ScalesEachPage()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdfWithPageSizes((200, 200), (400, 200), (200, 200));
            string outputDirectory = PdfTestAssetFactory.CreateTempOutputDirectory();

            try
            {
                await _service.SavePdfPagesToImagesAsync(
                    pdfPath,
                    pageIndexes: null,
                    outputDirectory,
                    saveAllPages: true,
                    ResolutionMode.Width,
                    value: 100,
                    format: OutputImageFormat.Png,
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.Equal((100, 100), ReadPixelSize(Path.Combine(outputDirectory, "page_1.png")));
                Assert.Equal((100, 50), ReadPixelSize(Path.Combine(outputDirectory, "page_2.png")));
                Assert.Equal((100, 100), ReadPixelSize(Path.Combine(outputDirectory, "page_3.png")));
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
                PdfTestAssetFactory.DeleteDirectoryIfExists(outputDirectory);
            }
        }

        /// <summary>
        /// 高さ指定モードでページサイズが混在する場合も、各ページが指定高さで保存されることを検証する
        /// </summary>
        [Fact]
        public async Task SavePdfPagesToImagesAsync_HeightModeMixedPageSizes_ScalesEachPage()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdfWithPageSizes((200, 200), (200, 400));
            string outputDirectory = PdfTestAssetFactory.CreateTempOutputDirectory();

            try
            {
                await _service.SavePdfPagesToImagesAsync(
                    pdfPath,
                    new[] { 1 },
                    outputDirectory,
                    saveAllPages: false,
                    ResolutionMode.Height,
                    value: 100,
                    format: OutputImageFormat.Png,
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.Equal((50, 100), ReadPixelSize(Path.Combine(outputDirectory, "page_2.png")));
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
                PdfTestAssetFactory.DeleteDirectoryIfExists(outputDirectory);
            }
        }

        /// <summary>
        /// ページサイズが整数でない場合 (Word 出力の A4 など) も、高さ指定の値どおりの高さで保存されることを検証する
        /// </summary>
        [Theory]
        [InlineData(1920)]
        [InlineData(1000)]
        [InlineData(1234)]
        public async Task SavePdfPagesToImagesAsync_HeightModeFractionalPageSize_MatchesRequestedHeight(int height)
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdfWithPageSizes((595.92, 841.92));
            string outputDirectory = PdfTestAssetFactory.CreateTempOutputDirectory();

            try
            {
                await _service.SavePdfPagesToImagesAsync(
                    pdfPath,
                    pageIndexes: null,
                    outputDirectory,
                    saveAllPages: true,
                    ResolutionMode.Height,
                    value: height,
                    format: OutputImageFormat.Png,
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.Equal(height, ReadPixelSize(Path.Combine(outputDirectory, "page_1.png")).Height);
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
                PdfTestAssetFactory.DeleteDirectoryIfExists(outputDirectory);
            }
        }

        /// <summary>
        /// ページサイズが整数でない場合も、幅指定の値どおりの幅で保存されることを検証する
        /// </summary>
        [Theory]
        [InlineData(1080)]
        [InlineData(1000)]
        [InlineData(777)]
        public async Task SavePdfPagesToImagesAsync_WidthModeFractionalPageSize_MatchesRequestedWidth(int width)
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdfWithPageSizes((595.92, 841.92));
            string outputDirectory = PdfTestAssetFactory.CreateTempOutputDirectory();

            try
            {
                await _service.SavePdfPagesToImagesAsync(
                    pdfPath,
                    pageIndexes: null,
                    outputDirectory,
                    saveAllPages: true,
                    ResolutionMode.Width,
                    value: width,
                    format: OutputImageFormat.Png,
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.Equal(width, ReadPixelSize(Path.Combine(outputDirectory, "page_1.png")).Width);
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
                    format: OutputImageFormat.Jpeg,
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.Equal(3, Directory.GetFiles(outputDirectory, "page_*.jpg").Length);
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
                PdfTestAssetFactory.DeleteDirectoryIfExists(outputDirectory);
            }
        }

        /// <summary>
        /// 総ページ数が 2 桁の PDF ではファイル名のページ番号が 2 桁にゼロ埋めされることを検証する
        /// </summary>
        [Fact]
        public async Task SavePdfPagesToImagesAsync_TwoDigitPageCount_ZeroPadsFileNames()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdf(pageCount: 10);
            string outputDirectory = PdfTestAssetFactory.CreateTempOutputDirectory();

            try
            {
                await _service.SavePdfPagesToImagesAsync(
                    pdfPath,
                    new[] { 0, 9 },
                    outputDirectory,
                    saveAllPages: false,
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.Equal(
                    new[] { "page_01.png", "page_10.png" },
                    Directory.GetFiles(outputDirectory)
                        .Select(Path.GetFileName)
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
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

                await Assert.ThrowsAsync<ArgumentException>(act);
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
            Assert.Equal(expected, PdfConversionService.ResolveSaveParallelism(processorCount, fileSizeBytes, maxPagePixels: 0, pixelBudget: 200_000_000));
        }

        /// <summary>
        /// 中サイズ PDF では並列度が最大 2 に制限されることを検証する
        /// </summary>
        [Theory]
        [InlineData(8, 16L * 1024 * 1024, 2)]
        [InlineData(2, 16L * 1024 * 1024, 2)]
        public void ResolveSaveParallelism_MediumPdf_CapsAtTwo(int processorCount, long fileSizeBytes, int expected)
        {
            Assert.Equal(expected, PdfConversionService.ResolveSaveParallelism(processorCount, fileSizeBytes, maxPagePixels: 0, pixelBudget: 200_000_000));
        }

        /// <summary>
        /// 大きい PDF では並列度が 1 に制限されることを検証する
        /// </summary>
        [Theory]
        [InlineData(8, 48L * 1024 * 1024, 1)]
        [InlineData(8, 100L * 1024 * 1024, 1)]
        public void ResolveSaveParallelism_LargePdf_IsSequential(int processorCount, long fileSizeBytes, int expected)
        {
            Assert.Equal(expected, PdfConversionService.ResolveSaveParallelism(processorCount, fileSizeBytes, maxPagePixels: 0, pixelBudget: 200_000_000));
        }

        /// <summary>
        /// 描画サイズが大きいほど、同時に描画する画素数の合計が予算に収まるよう並列度が下がることを検証する
        /// </summary>
        [Theory]
        [InlineData(10_000_000L, 4)]
        [InlineData(50_000_000L, 4)]
        [InlineData(60_000_000L, 3)]
        [InlineData(100_000_000L, 2)]
        [InlineData(139_000_000L, 1)]
        [InlineData(300_000_000L, 1)]
        public void ResolveSaveParallelism_LargeRenderSize_LimitsByPixelBudget(long maxPagePixels, int expected)
        {
            Assert.Equal(expected, PdfConversionService.ResolveSaveParallelism(8, 1 * 1024 * 1024, maxPagePixels, pixelBudget: 200_000_000));
        }

        /// <summary>
        /// ファイルサイズによる制限と描画サイズによる制限のうち、小さい方が採用されることを検証する
        /// </summary>
        [Fact]
        public void ResolveSaveParallelism_FileSizeAndRenderSize_UsesSmallerLimit()
        {
            Assert.Equal(2, PdfConversionService.ResolveSaveParallelism(8, 16L * 1024 * 1024, 10_000_000, pixelBudget: 200_000_000));
            Assert.Equal(1, PdfConversionService.ResolveSaveParallelism(8, 16L * 1024 * 1024, 150_000_000, pixelBudget: 200_000_000));
        }

        /// <summary>
        /// 描画サイズが上限を超えるページを含む場合、InvalidOperationException で中断し、上限内のページも書き出さないことを検証する
        /// </summary>
        [Fact]
        public async Task SavePdfPagesToImagesAsync_PageExceedingRenderLimit_ThrowsAndDoesNotWriteFiles()
        {
            // 1 ページ目は 1200 DPI でも上限内、2 ページ目 (14400pt 四方) は 1200 DPI で 240000x240000 px になる
            string pdfPath = PdfTestAssetFactory.CreateTempPdfWithPageSizes((200, 200), (14400, 14400));
            string outputDirectory = PdfTestAssetFactory.CreateTempOutputDirectory();

            try
            {
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SavePdfPagesToImagesAsync(
                    pdfPath,
                    pageIndexes: null,
                    outputDirectory,
                    saveAllPages: true,
                    ResolutionMode.Dpi,
                    value: 1200,
                    format: OutputImageFormat.Png,
                    cancellationToken: TestContext.Current.CancellationToken));

                Assert.Contains("ページ 2", exception.Message);
                Assert.Empty(Directory.GetFiles(outputDirectory));
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
                PdfTestAssetFactory.DeleteDirectoryIfExists(outputDirectory);
            }
        }

        /// <summary>
        /// プレビューでも描画サイズが上限を超える場合は InvalidOperationException になることを検証する
        /// </summary>
        [Fact]
        public async Task ConvertPdfPageToImageAsync_PageExceedingRenderLimit_ThrowsInvalidOperationException()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdfWithPageSizes((14400, 14400));

            try
            {
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => _service.ConvertPdfPageToImageAsync(pdfPath, pageIndex: 0, ResolutionMode.Dpi, value: 1200, cancellationToken: TestContext.Current.CancellationToken));
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
            }
        }

        /// <summary>
        /// 出力解像度が最大サイズより大きい場合は、縦横比を保って最大サイズに収まるよう縮小したプレビューを返すことを検証する
        /// </summary>
        [Fact]
        public async Task RenderPdfPagePreviewAsync_OutputLargerThanMaxSize_DownscalesToFit()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdfWithPageSizes((200, 100));

            try
            {
                StaTestHelper.Run(async () =>
                {
                    // 300 DPI の出力は 833x416 px。最大 100x100 px に収めると幅 100 px・高さ 50 px になる
                    PdfPagePreview preview = await _service.RenderPdfPagePreviewAsync(
                        pdfPath,
                        pageIndex: 0,
                        ResolutionMode.Dpi,
                        value: 300,
                        preserveTransparency: true,
                        maxPixelWidth: 100,
                        maxPixelHeight: 100,
                        TestContext.Current.CancellationToken);

                    Assert.Equal(833, preview.OutputPixelWidth);
                    Assert.Equal(416, preview.OutputPixelHeight);
                    Assert.True(preview.IsDownscaled);
                    Assert.InRange(preview.Image.PixelWidth, 99, 100);
                    Assert.InRange(preview.Image.PixelHeight, 49, 50);
                });
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
            }
        }

        /// <summary>
        /// 出力解像度が最大サイズに収まる場合は、拡大せず出力解像度のままプレビューを返すことを検証する
        /// </summary>
        [Fact]
        public async Task RenderPdfPagePreviewAsync_OutputWithinMaxSize_RendersAtOutputResolution()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdf();

            try
            {
                StaTestHelper.Run(async () =>
                {
                    PdfPagePreview preview = await _service.RenderPdfPagePreviewAsync(
                        pdfPath,
                        pageIndex: 0,
                        ResolutionMode.Width,
                        value: 120,
                        preserveTransparency: true,
                        maxPixelWidth: 1000,
                        maxPixelHeight: 1000,
                        TestContext.Current.CancellationToken);

                    Assert.Equal(120, preview.Image.PixelWidth);
                    Assert.Equal(120, preview.Image.PixelHeight);
                    Assert.Equal(120, preview.OutputPixelWidth);
                    Assert.False(preview.IsDownscaled);
                });
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
            }
        }

        /// <summary>
        /// 出力解像度での描画サイズが上限を超える場合は、縮小すれば描画できても InvalidOperationException になることを検証する
        /// </summary>
        [Fact]
        public async Task RenderPdfPagePreviewAsync_PageExceedingRenderLimit_ThrowsInvalidOperationException()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdfWithPageSizes((14400, 14400));

            try
            {
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => _service.RenderPdfPagePreviewAsync(pdfPath, 0, ResolutionMode.Dpi, 1200, true, 100, 100, TestContext.Current.CancellationToken));
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
            }
        }

        /// <summary>
        /// 最大サイズに 1 未満を指定した場合は ArgumentOutOfRangeException になることを検証する
        /// </summary>
        [Theory]
        [InlineData(0, 100)]
        [InlineData(100, 0)]
        public async Task RenderPdfPagePreviewAsync_InvalidMaxSize_ThrowsArgumentOutOfRangeException(int maxPixelWidth, int maxPixelHeight)
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdf();

            try
            {
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                    () => _service.RenderPdfPagePreviewAsync(pdfPath, 0, ResolutionMode.Default, 0, true, maxPixelWidth, maxPixelHeight, TestContext.Current.CancellationToken));
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
            }
        }

        /// <summary>
        /// プレビューの描画倍率は、出力解像度を上限に最大サイズへ収まる倍率になることを検証する
        /// </summary>
        [Theory]
        [InlineData(200, 200, 4.0, 100, 100, 0.5)]
        [InlineData(200, 100, 4.0, 100, 100, 0.5)]
        [InlineData(200, 200, 1.0, 1000, 1000, 1.0)]
        [InlineData(1000, 1, 1.0, 10, 10, 1.0)]
        public void ComputePreviewScale_ReturnsScaleWithinMaxSize(double pageWidth, double pageHeight, double renderScale, int maxPixelWidth, int maxPixelHeight, double expected)
        {
            double scale = PdfConversionService.ComputePreviewScale(pageWidth, pageHeight, renderScale, maxPixelWidth, maxPixelHeight);

            Assert.Equal(expected, scale, precision: 9);
        }

        /// <summary>
        /// メモリキャッシュの上限を超える PDF でも、ページ数の取得とページの描画ができることを検証する
        /// </summary>
        [Fact]
        public async Task ConvertPdfPageToImageAsync_FileLargerThanCacheLimit_RendersPage()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdfWithPadding(LargePdfPaddingBytes, pageCount: 3);

            try
            {
                int pageCount = await _service.GetPdfPageCountAsync(pdfPath, TestContext.Current.CancellationToken);

                StaTestHelper.Run(async () =>
                {
                    BitmapSource bitmap = await _service.ConvertPdfPageToImageAsync(pdfPath, pageIndex: 2, cancellationToken: TestContext.Current.CancellationToken);

                    Assert.Equal(200, bitmap.PixelWidth);
                    Assert.Equal(200, bitmap.PixelHeight);
                });
                Assert.Equal(3, pageCount);
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
            }
        }

        /// <summary>
        /// メモリキャッシュの上限を超える PDF でも、全ページを画像ファイルとして保存できることを検証する
        /// </summary>
        [Fact]
        public async Task SavePdfPagesToImagesAsync_FileLargerThanCacheLimit_WritesAllPageFiles()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdfWithPadding(LargePdfPaddingBytes, pageCount: 3);
            string outputDirectory = PdfTestAssetFactory.CreateTempOutputDirectory();

            try
            {
                await _service.SavePdfPagesToImagesAsync(
                    pdfPath,
                    pageIndexes: null,
                    outputDirectory,
                    saveAllPages: true,
                    format: OutputImageFormat.Png,
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(File.Exists(Path.Combine(outputDirectory, "page_1.png")));
                Assert.True(File.Exists(Path.Combine(outputDirectory, "page_2.png")));
                Assert.True(File.Exists(Path.Combine(outputDirectory, "page_3.png")));
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
                PdfTestAssetFactory.DeleteDirectoryIfExists(outputDirectory);
            }
        }

        /// <summary>
        /// メモリキャッシュの上限を超える PDF ヘッダーのないファイルは、保存でも InvalidDataException になり何も書き出さないことを検証する
        /// </summary>
        [Fact]
        public async Task SavePdfPagesToImagesAsync_LargeFileWithoutPdfHeader_ThrowsInvalidDataException()
        {
            string path = PdfTestAssetFactory.CreateTempInvalidPdfFileOfSize(LargePdfPaddingBytes);
            string outputDirectory = PdfTestAssetFactory.CreateTempOutputDirectory();

            try
            {
                await Assert.ThrowsAsync<InvalidDataException>(() => _service.SavePdfPagesToImagesAsync(
                    path,
                    pageIndexes: null,
                    outputDirectory,
                    saveAllPages: true,
                    cancellationToken: TestContext.Current.CancellationToken));

                Assert.Empty(Directory.GetFiles(outputDirectory));
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(path);
                PdfTestAssetFactory.DeleteDirectoryIfExists(outputDirectory);
            }
        }

        /// <summary>
        /// メモリキャッシュの上限を超える PDF を開いたままでも、ファイルを削除できることを検証する
        /// </summary>
        [Fact]
        public async Task GetPdfPageCountAsync_FileLargerThanCacheLimit_DoesNotBlockDeletion()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdfWithPadding(LargePdfPaddingBytes);

            try
            {
                await _service.GetPdfPageCountAsync(pdfPath, TestContext.Current.CancellationToken);

                Exception exception = Record.Exception(() => File.Delete(pdfPath));

                Assert.Null(exception);
            }
            finally
            {
                _service.Dispose();
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
            }
        }

        /// <summary>
        /// メモリキャッシュの上限を超える PDF ヘッダーのないファイルは InvalidDataException になることを検証する
        /// </summary>
        [Fact]
        public async Task GetPdfPageCountAsync_LargeFileWithoutPdfHeader_ThrowsInvalidDataException()
        {
            string path = PdfTestAssetFactory.CreateTempInvalidPdfFileOfSize(LargePdfPaddingBytes);

            try
            {
                await Assert.ThrowsAsync<InvalidDataException>(
                    () => _service.GetPdfPageCountAsync(path, TestContext.Current.CancellationToken));
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(path);
            }
        }

        /// <summary>
        /// 開いたままの文書があっても、ファイルが更新された場合は開き直して新しい内容を返すことを検証する
        /// </summary>
        [Fact]
        public async Task GetPdfPageCountAsync_FileUpdatedAfterOpen_ReturnsUpdatedPageCount()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdf(pageCount: 2);
            string updatedPath = PdfTestAssetFactory.CreateTempPdf(pageCount: 3);

            try
            {
                int before = await _service.GetPdfPageCountAsync(pdfPath, TestContext.Current.CancellationToken);
                DateTime lastWrite = File.GetLastWriteTimeUtc(pdfPath);
                File.Copy(updatedPath, pdfPath, overwrite: true);
                File.SetLastWriteTimeUtc(pdfPath, lastWrite.AddMinutes(1));

                int after = await _service.GetPdfPageCountAsync(pdfPath, TestContext.Current.CancellationToken);

                Assert.Equal(2, before);
                Assert.Equal(3, after);
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
                PdfTestAssetFactory.DeleteIfExists(updatedPath);
            }
        }

        /// <summary>
        /// 破棄後の呼び出しは ObjectDisposedException になることを検証する
        /// </summary>
        [Fact]
        public async Task GetPdfPageCountAsync_AfterDispose_ThrowsObjectDisposedException()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdf();

            try
            {
                _service.Dispose();

                await Assert.ThrowsAsync<ObjectDisposedException>(
                    () => _service.GetPdfPageCountAsync(pdfPath, TestContext.Current.CancellationToken));
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
            }
        }

        /// <summary>
        /// キャンセル済みトークンでは保存処理が OperationCanceledException で中断し、出力ファイルを作成しないことを検証する
        /// </summary>
        [Fact]
        public async Task SavePdfPagesToImagesAsync_CancelledToken_ThrowsAndDoesNotWriteFiles()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdf(pageCount: 2);
            string outputDirectory = PdfTestAssetFactory.CreateTempOutputDirectory();

            try
            {
                using (var cts = new CancellationTokenSource())
                {
                    cts.Cancel();

                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.SavePdfPagesToImagesAsync(
                        pdfPath,
                        new[] { 0, 1 },
                        outputDirectory,
                        saveAllPages: false,
                        cancellationToken: cts.Token));
                }

                Assert.Empty(Directory.GetFiles(outputDirectory));
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
                PdfTestAssetFactory.DeleteDirectoryIfExists(outputDirectory);
            }
        }

        /// <summary>
        /// 保存途中でキャンセルされた場合、OperationCanceledException で中断し、保存先に部分出力を残さず既存ファイルも維持することを検証する
        /// </summary>
        [Fact]
        public async Task SavePdfPagesToImagesAsync_CancelledMidway_ThrowsAndDoesNotLeavePartialOutput()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdf(pageCount: 8);
            string outputDirectory = PdfTestAssetFactory.CreateTempOutputDirectory();
            string existingPath = Path.Combine(outputDirectory, "page_1.png");
            const string existingContent = "keep-me";
            File.WriteAllText(existingPath, existingContent);

            try
            {
                using (var cts = new CancellationTokenSource())
                {
                    var progress = new SynchronousProgress<SaveProgressReport>(_ => cts.Cancel());

                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.SavePdfPagesToImagesAsync(
                        pdfPath,
                        pageIndexes: null,
                        outputDirectory,
                        saveAllPages: true,
                        format: OutputImageFormat.Png,
                        progress: progress,
                        cancellationToken: cts.Token));
                }

                Assert.Equal(existingContent, File.ReadAllText(existingPath));
                Assert.Equal(
                    new[] { existingPath },
                    Directory.GetFiles(outputDirectory).OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
                PdfTestAssetFactory.DeleteDirectoryIfExists(outputDirectory);
            }
        }

        /// <summary>
        /// 保存先への反映が途中で失敗した場合、反映済みのファイルを元に戻し、一時ファイルも残さないことを検証する
        /// </summary>
        [Fact]
        public async Task SavePdfPagesToImagesAsync_CommitFailsMidway_RestoresExistingFilesAndLeavesNoTemporaryFiles()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdf(pageCount: 2);
            string outputDirectory = PdfTestAssetFactory.CreateTempOutputDirectory();
            string existingPath = Path.Combine(outputDirectory, "page_1.png");
            const string existingContent = "keep-me";
            File.WriteAllText(existingPath, existingContent);

            // 2 ページ目の出力先をディレクトリにして、1 ページ目の反映後に失敗させる
            string blockingDirectory = Path.Combine(outputDirectory, "page_2.png");
            Directory.CreateDirectory(blockingDirectory);

            try
            {
                Exception exception = await Record.ExceptionAsync(() => _service.SavePdfPagesToImagesAsync(
                    pdfPath,
                    new[] { 0, 1 },
                    outputDirectory,
                    saveAllPages: false,
                    format: OutputImageFormat.Png,
                    cancellationToken: TestContext.Current.CancellationToken));

                Assert.NotNull(exception);
                Assert.Equal(existingContent, File.ReadAllText(existingPath));
                Assert.Equal(
                    new[] { existingPath },
                    Directory.GetFiles(outputDirectory).OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
                Assert.Empty(Directory.GetFileSystemEntries(blockingDirectory));
                Assert.Equal(new[] { blockingDirectory }, Directory.GetDirectories(outputDirectory));
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
                PdfTestAssetFactory.DeleteDirectoryIfExists(outputDirectory);
            }
        }

        /// <summary>
        /// 保存成功時は既存の同名ファイルを上書きし、一時ファイルを残さないことを検証する
        /// </summary>
        [Fact]
        public async Task SavePdfPagesToImagesAsync_Success_OverwritesExistingFiles()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdf(pageCount: 1);
            string outputDirectory = PdfTestAssetFactory.CreateTempOutputDirectory();
            string existingPath = Path.Combine(outputDirectory, "page_1.png");
            File.WriteAllText(existingPath, "old-content");

            try
            {
                await _service.SavePdfPagesToImagesAsync(
                    pdfPath,
                    new[] { 0 },
                    outputDirectory,
                    saveAllPages: false,
                    format: OutputImageFormat.Png,
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(File.Exists(existingPath));
                Assert.NotEqual("old-content", File.ReadAllText(existingPath));
                Assert.True(new FileInfo(existingPath).Length > 0);
                Assert.Equal(new[] { existingPath }, Directory.GetFiles(outputDirectory));
                Assert.Empty(Directory.GetDirectories(outputDirectory));
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
                PdfTestAssetFactory.DeleteDirectoryIfExists(outputDirectory);
            }
        }

        /// <summary>
        /// 存在しない保存先でも保存でき、ステージングディレクトリを残さないことを検証する
        /// </summary>
        [Fact]
        public async Task SavePdfPagesToImagesAsync_MissingOutputDirectory_CreatesItAndLeavesNoStagingDirectory()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdf(pageCount: 2);
            string baseDirectory = PdfTestAssetFactory.CreateTempOutputDirectory();
            string outputDirectory = Path.Combine(baseDirectory, "nested", "output");

            try
            {
                await _service.SavePdfPagesToImagesAsync(
                    pdfPath,
                    pageIndexes: null,
                    outputDirectory,
                    saveAllPages: true,
                    format: OutputImageFormat.Png,
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.Equal(2, Directory.GetFiles(outputDirectory, "page_*.png").Length);
                Assert.Empty(Directory.GetDirectories(outputDirectory));
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
                PdfTestAssetFactory.DeleteDirectoryIfExists(baseDirectory);
            }
        }

        /// <summary>
        /// 保存中のステージングディレクトリは隠しフォルダーであることを検証する
        /// </summary>
        [Fact]
        public async Task SavePdfPagesToImagesAsync_WhileSaving_StagingDirectoryIsHidden()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdf(pageCount: 1);
            string outputDirectory = PdfTestAssetFactory.CreateTempOutputDirectory();
            int reportCount = 0;
            var progress = new SynchronousProgress<SaveProgressReport>(_ =>
            {
                reportCount++;
                string[] stagingDirectories = Directory.GetDirectories(outputDirectory, ".pdfconverter-staging-*");
                Assert.Single(stagingDirectories);
                Assert.True(new DirectoryInfo(stagingDirectories[0]).Attributes.HasFlag(FileAttributes.Hidden));
            });

            try
            {
                await _service.SavePdfPagesToImagesAsync(
                    pdfPath,
                    pageIndexes: null,
                    outputDirectory,
                    saveAllPages: true,
                    format: OutputImageFormat.Png,
                    progress: progress,
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(reportCount > 0);
                Assert.Empty(Directory.GetDirectories(outputDirectory));
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
                PdfTestAssetFactory.DeleteDirectoryIfExists(outputDirectory);
            }
        }

        /// <summary>
        /// 強制終了で残った古いステージングディレクトリだけを、次の保存時に削除することを検証する
        /// </summary>
        [Fact]
        public async Task SavePdfPagesToImagesAsync_StaleStagingDirectory_DeletesOnlyOldStagingDirectories()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdf(pageCount: 1);
            string outputDirectory = PdfTestAssetFactory.CreateTempOutputDirectory();
            string staleDirectory = Path.Combine(outputDirectory, ".pdfconverter-staging-" + Guid.NewGuid().ToString("N"));
            string recentDirectory = Path.Combine(outputDirectory, ".pdfconverter-staging-" + Guid.NewGuid().ToString("N"));
            string otherNameDirectory = Path.Combine(outputDirectory, ".pdfconverter-staging-keep");
            foreach (string directory in new[] { staleDirectory, recentDirectory, otherNameDirectory })
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "page_1.png"), "x");
            }

            Directory.SetLastWriteTimeUtc(staleDirectory, DateTime.UtcNow.AddDays(-2));
            Directory.SetLastWriteTimeUtc(otherNameDirectory, DateTime.UtcNow.AddDays(-2));

            try
            {
                await _service.SavePdfPagesToImagesAsync(
                    pdfPath,
                    pageIndexes: null,
                    outputDirectory,
                    saveAllPages: true,
                    format: OutputImageFormat.Png,
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.False(Directory.Exists(staleDirectory));
                Assert.True(Directory.Exists(recentDirectory));
                Assert.True(Directory.Exists(otherNameDirectory));
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
                PdfTestAssetFactory.DeleteDirectoryIfExists(outputDirectory);
            }
        }

        /// <summary>
        /// 存在しない保存先への保存が途中でキャンセルされた場合、この保存で作成したフォルダーを残さないことを検証する
        /// </summary>
        [Fact]
        public async Task SavePdfPagesToImagesAsync_CancelledMidwayWithMissingOutputDirectory_RemovesCreatedDirectories()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdf(pageCount: 8);
            string baseDirectory = PdfTestAssetFactory.CreateTempOutputDirectory();
            string outputDirectory = Path.Combine(baseDirectory, "nested", "output");

            try
            {
                using (var cts = new CancellationTokenSource())
                {
                    var progress = new SynchronousProgress<SaveProgressReport>(_ => cts.Cancel());

                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.SavePdfPagesToImagesAsync(
                        pdfPath,
                        pageIndexes: null,
                        outputDirectory,
                        saveAllPages: true,
                        format: OutputImageFormat.Png,
                        progress: progress,
                        cancellationToken: cts.Token));
                }

                Assert.True(Directory.Exists(baseDirectory));
                Assert.Empty(Directory.GetFileSystemEntries(baseDirectory));
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
                PdfTestAssetFactory.DeleteDirectoryIfExists(baseDirectory);
            }
        }

        /// <summary>
        /// 呼び出し元の同期コンテキスト (UI スレッド相当) へ継続処理を戻さずに保存が完了することを検証する
        /// </summary>
        [Fact]
        public void SavePdfPagesToImagesAsync_WithSynchronizationContext_DoesNotPostContinuations()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPdf(pageCount: 2);
            string outputDirectory = PdfTestAssetFactory.CreateTempOutputDirectory();
            var context = new NonPumpingSynchronizationContext();
            SynchronizationContext previousContext = SynchronizationContext.Current;

            try
            {
                Task saveTask;
                SynchronizationContext.SetSynchronizationContext(context);
                try
                {
                    saveTask = _service.SavePdfPagesToImagesAsync(
                        pdfPath,
                        pageIndexes: null,
                        outputDirectory,
                        saveAllPages: true,
                        format: OutputImageFormat.Png,
                        cancellationToken: TestContext.Current.CancellationToken);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousContext);
                }

                // 継続が同期コンテキストへ戻らないことを検証するため、await せずに同期で待機する
#pragma warning disable xUnit1031
                Assert.True(saveTask.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken), "保存処理が呼び出し元の同期コンテキストへ戻ろうとして完了しませんでした。");
#pragma warning restore xUnit1031
                Assert.Equal(0, context.PostCount);
                Assert.Equal(2, Directory.GetFiles(outputDirectory, "page_*.png").Length);
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
                PdfTestAssetFactory.DeleteDirectoryIfExists(outputDirectory);
            }
        }


        /// <summary>
        /// パスワードで保護された PDF のページ数取得は、パスワード未設定なら PdfPasswordRequiredException になることを検証する
        /// </summary>
        [Fact]
        public async Task GetPdfPageCountAsync_PasswordProtectedWithoutPassword_ThrowsPasswordRequiredException()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPasswordProtectedPdf("correct");
            try
            {
                var exception = await Assert.ThrowsAsync<PdfPasswordRequiredException>(
                    () => _service.GetPdfPageCountAsync(pdfPath, TestContext.Current.CancellationToken));
                Assert.False(exception.WasPasswordProvided);
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
            }
        }

        /// <summary>
        /// 誤ったパスワードを設定すると、パスワード指定済みを示す PdfPasswordRequiredException になることを検証する
        /// </summary>
        [Fact]
        public async Task GetPdfPageCountAsync_WrongPassword_ThrowsPasswordRequiredExceptionWithProvidedFlag()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPasswordProtectedPdf("correct");
            try
            {
                _service.SetPdfPassword(pdfPath, "wrong");

                var exception = await Assert.ThrowsAsync<PdfPasswordRequiredException>(
                    () => _service.GetPdfPageCountAsync(pdfPath, TestContext.Current.CancellationToken));
                Assert.True(exception.WasPasswordProvided);
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
            }
        }

        /// <summary>
        /// 正しいパスワードを設定すると、ページ数取得・プレビュー・保存ができることを検証する
        /// </summary>
        [Fact]
        public async Task SetPdfPassword_CorrectPassword_OpensPdfForCountPreviewAndSave()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPasswordProtectedPdf("correct");
            string outputDir = PdfTestAssetFactory.CreateTempOutputDirectory();
            try
            {
                await Assert.ThrowsAsync<PdfPasswordRequiredException>(
                    () => _service.GetPdfPageCountAsync(pdfPath, TestContext.Current.CancellationToken));

                _service.SetPdfPassword(pdfPath, "correct");

                Assert.Equal(1, await _service.GetPdfPageCountAsync(pdfPath, TestContext.Current.CancellationToken));

                BitmapSource bitmap = await _service.ConvertPdfPageToImageAsync(pdfPath, pageIndex: 0, cancellationToken: TestContext.Current.CancellationToken);
                Assert.True(bitmap.PixelWidth > 0);

                await _service.SavePdfPagesToImagesAsync(
                    pdfPath,
                    pageIndexes: null,
                    outputDir,
                    saveAllPages: true,
                    cancellationToken: TestContext.Current.CancellationToken);
                Assert.Single(Directory.GetFiles(outputDir));
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
                PdfTestAssetFactory.DeleteDirectoryIfExists(outputDir);
            }
        }

        /// <summary>
        /// 設定したパスワードは別のファイルには使われないことを検証する
        /// </summary>
        [Fact]
        public async Task SetPdfPassword_OtherFile_DoesNotApplyPassword()
        {
            string protectedPath = PdfTestAssetFactory.CreateTempPasswordProtectedPdf("correct");
            string otherPath = PdfTestAssetFactory.CreateTempPasswordProtectedPdf("correct");
            try
            {
                _service.SetPdfPassword(protectedPath, "correct");

                await Assert.ThrowsAsync<PdfPasswordRequiredException>(
                    () => _service.GetPdfPageCountAsync(otherPath, TestContext.Current.CancellationToken));
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(protectedPath);
                PdfTestAssetFactory.DeleteIfExists(otherPath);
            }
        }

        /// <summary>
        /// 大文字小文字だけ異なるパスでもパスワードが使われることを検証する
        /// </summary>
        [Fact]
        public async Task SetPdfPassword_PathWithDifferentCase_AppliesPassword()
        {
            string pdfPath = PdfTestAssetFactory.CreateTempPasswordProtectedPdf("correct");
            try
            {
                _service.SetPdfPassword(pdfPath, "correct");

                Assert.Equal(1, await _service.GetPdfPageCountAsync(pdfPath.ToUpperInvariant(), TestContext.Current.CancellationToken));
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
            }
        }

        /// <summary>
        /// 別のファイルを開いた時点で、保持していたパスワードが破棄されることを検証する
        /// </summary>
        [Fact]
        public async Task GetPdfPageCountAsync_OtherFileOpened_DiscardsPassword()
        {
            string protectedPath = PdfTestAssetFactory.CreateTempPasswordProtectedPdf("correct");
            string otherPath = PdfTestAssetFactory.CreateTempPdf();
            try
            {
                _service.SetPdfPassword(protectedPath, "correct");

                await _service.GetPdfPageCountAsync(otherPath, TestContext.Current.CancellationToken);

                await Assert.ThrowsAsync<PdfPasswordRequiredException>(
                    () => _service.GetPdfPageCountAsync(protectedPath, TestContext.Current.CancellationToken));
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(protectedPath);
                PdfTestAssetFactory.DeleteIfExists(otherPath);
            }
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// 画像ファイルのピクセルサイズを読み取る
        /// </summary>
        private static (int Width, int Height) ReadPixelSize(string imagePath)
        {
            using (var stream = File.OpenRead(imagePath))
            {
                BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                BitmapFrame frame = decoder.Frames[0];
                return (frame.PixelWidth, frame.PixelHeight);
            }
        }


        /********************************************************************************/
        /*                                   ネスト型                                   */
        /********************************************************************************/
        /// <summary>
        /// Post された継続処理を実行せずに件数だけ記録する同期コンテキスト
        /// </summary>
        private sealed class NonPumpingSynchronizationContext : SynchronizationContext
        {
            private int _postCount;

            /// <summary>Post された回数</summary>
            public int PostCount => Volatile.Read(ref _postCount);

            /// <inheritdoc/>
            public override void Post(SendOrPostCallback d, object state)
            {
                Interlocked.Increment(ref _postCount);
            }
        }
    }
}
