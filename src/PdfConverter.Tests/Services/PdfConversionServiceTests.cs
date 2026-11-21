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
    public class PdfConversionServiceTests
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        private readonly PdfConversionService _service = new PdfConversionService();


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/

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
        /// 保存成功時は既存の同名ファイルを上書きすることを検証する
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
            }
            finally
            {
                PdfTestAssetFactory.DeleteIfExists(pdfPath);
                PdfTestAssetFactory.DeleteDirectoryIfExists(outputDirectory);
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
