using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Docnet.Core;
using Docnet.Core.Models;
using Docnet.Core.Readers;
using PdfConverter.Models;

namespace PdfConverter.Services
{
    /// <summary>
    /// Docnet.Core(PDFium)を使用してPDFページを画像に変換・保存するサービス
    /// </summary>
    /// <remarks>
    /// 同一ファイルへの連続アクセス時のディスク I/O を削減するため、
    /// 一定サイズ以下のPDFのみメモリキャッシュする<br/>
    /// 並列保存時はワーカーごとにDocReaderがPDFを保持するため、
    /// ファイルサイズに応じて並列度を制限しメモリピークを抑える
    /// </remarks>
    public class PdfConversionService : IPdfConversionService
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>メモリキャッシュの上限(バイト)</summary>
        private const long MaxCacheableFileSizeBytes = 64L * 1024 * 1024;

        /// <summary>保存処理の並列ワーカー数の上限</summary>
        private const int MaxSaveParallelism = 4;

        /// <summary>フル並列を許可するPDFサイズの上限(バイト)</summary>
        private const long SmallPdfThresholdBytes = 8L * 1024 * 1024;

        /// <summary>並列度を2に制限するPDFサイズの上限(バイト)</summary>
        private const long MediumPdfThresholdBytes = 32L * 1024 * 1024;

        /// <summary>キャッシュの読み書きを保護するロックオブジェクト</summary>
        private readonly object _cacheLock = new object();

        /// <summary>現在キャッシュされているファイルのパス</summary>
        private string _cachedFilePath;

        /// <summary>キャッシュされたファイルの生バイト列</summary>
        private byte[] _cachedFileBytes;

        /// <summary>キャッシュ取得時点のファイル最終更新日時(UTC)</summary>
        private DateTime _cachedFileLastWrite;


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        public async Task<BitmapSource> ConvertPdfPageToImageAsync(string filePath, int pageIndex, ResolutionMode mode = ResolutionMode.Default, double value = 0, bool preserveTransparency = true, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("PDFファイルが見つかりません。", filePath);
            }

            byte[] fileBytes = await GetFileBytesAsync(filePath, cancellationToken);

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (var docReader = DocLib.Instance.GetDocReader(fileBytes, new PageDimensions(1.0)))
                {
                    int pageCount = docReader.GetPageCount();

                    if (pageIndex < 0 || pageIndex >= pageCount)
                    {
                        throw new ArgumentOutOfRangeException(nameof(pageIndex), "ページインデックスが範囲外です。");
                    }

                    return RenderScaledPage(docReader, pageIndex, mode, value, preserveTransparency, cancellationToken);
                }
            }, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<int> GetPdfPageCountAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("PDFファイルが見つかりません。", filePath);
            }

            byte[] fileBytes = await GetFileBytesAsync(filePath, cancellationToken);

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (var docReader = DocLib.Instance.GetDocReader(fileBytes, new PageDimensions(1.0)))
                {
                    return docReader.GetPageCount();
                }
            }, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task SavePdfPagesToImagesAsync(string filePath, IEnumerable<int> pageIndexes, string folderPath, bool saveAllPages, ResolutionMode mode = ResolutionMode.Default, double value = 0, OutputImageFormat format = OutputImageFormat.Png, bool preserveTransparency = true, IProgress<SaveProgressReport> progress = null, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("PDFファイルが見つかりません。", filePath);
            }

            byte[] fileBytes = await GetFileBytesAsync(filePath, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            int pageCount;
            using (var docReader = DocLib.Instance.GetDocReader(fileBytes, new PageDimensions(1.0)))
            {
                pageCount = docReader.GetPageCount();
            }

            List<int> pagesToSave = ResolvePagesToSave(pageIndexes, saveAllPages, pageCount);
            int total = pagesToSave.Count;
            int completedCount = 0;

            // ワーカーごとに1つのDocReaderを再利用し、ページごとの生成コストを削減する
            // 大容量PDFではDocReaderごとのネイティブメモリが積み上がるため、ファイルサイズに応じて並列度を抑える
            int maxParallelism = ResolveSaveParallelism(Environment.ProcessorCount, fileBytes.Length);
            IReadOnlyList<IReadOnlyList<int>> partitions = PartitionPages(pagesToSave, maxParallelism);

            var tasks = partitions
                .Where(partition => partition.Count > 0)
                .Select(partition => Task.Run(() =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    using (var docReader = DocLib.Instance.GetDocReader(fileBytes, new PageDimensions(1.0)))
                    {
                        foreach (int pageIndex in partition)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                return;
                            }

                            SavePageToFile(docReader, pageIndex, folderPath, mode, value, format, preserveTransparency);
                            int completed = Interlocked.Increment(ref completedCount);
                            progress?.Report(new SaveProgressReport(completed * 100.0 / total, $"保存中... {completed}/{total} ページ"));
                        }
                    }
                }));

            await Task.WhenAll(tasks);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// 保存対象ページ一覧を解決する
        /// </summary>
        /// <param name="pageIndexes">保存対象のページインデックス一覧</param>
        /// <param name="saveAllPages"><c>true</c>の場合は全ページを保存する</param>
        /// <param name="pageCount">PDFのページ数</param>
        /// <returns>保存対象のページインデックス一覧</returns>
        /// <exception cref="ArgumentException">有効なページインデックス一覧または"全ページを保存"を選択してください</exception>
        /// <exception cref="ArgumentOutOfRangeException">範囲外のページインデックスが含まれている場合</exception>
        private static List<int> ResolvePagesToSave(IEnumerable<int> pageIndexes, bool saveAllPages, int pageCount)
        {
            if (saveAllPages)
            {
                return Enumerable.Range(0, pageCount).ToList();
            }

            if (pageIndexes == null)
            {
                throw new ArgumentException("有効なページインデックス一覧または'全ページを保存'を選択してください。");
            }

            List<int> pagesToSave = pageIndexes.Distinct().OrderBy(x => x).ToList();
            if (pagesToSave.Count == 0)
            {
                throw new ArgumentException("保存対象のページが指定されていません。", nameof(pageIndexes));
            }

            if (pagesToSave.Any(index => index < 0 || index >= pageCount))
            {
                throw new ArgumentOutOfRangeException(nameof(pageIndexes), "範囲外のページインデックスが含まれています。PDFのページ数内で指定してください。");
            }

            return pagesToSave;
        }

        /// <summary>
        /// 保存処理の並列ワーカー数をCPUコア数とPDFサイズから決定する
        /// </summary>
        /// <param name="processorCount">利用可能な論理プロセッサ数</param>
        /// <param name="fileSizeBytes">PDFファイルサイズ(バイト)</param>
        /// <returns>1以上の並列ワーカー数</returns>
        internal static int ResolveSaveParallelism(int processorCount, long fileSizeBytes)
        {
            int cpuBound = Math.Max(1, Math.Min(processorCount, MaxSaveParallelism));

            if (fileSizeBytes <= SmallPdfThresholdBytes)
            {
                return cpuBound;
            }

            if (fileSizeBytes <= MediumPdfThresholdBytes)
            {
                return Math.Min(cpuBound, 2);
            }

            if (fileSizeBytes <= MaxCacheableFileSizeBytes)
            {
                return 1;
            }

            return 1;
        }

        /// <summary>
        /// ページ一覧を並列ワーカー数に応じて分割する
        /// </summary>
        /// <param name="pages">0始まりのページインデックス一覧</param>
        /// <param name="partitionCount">分割するパーティション数</param>
        /// <returns>分割されたページインデックスのリスト</returns>
        private static IReadOnlyList<IReadOnlyList<int>> PartitionPages(IReadOnlyList<int> pages, int partitionCount)
        {
            var partitions = Enumerable.Range(0, partitionCount)
                .Select(_ => new List<int>())
                .ToList();

            for (int i = 0; i < pages.Count; i++)
            {
                partitions[i % partitionCount].Add(pages[i]);
            }

            return partitions;
        }

        /// <summary>
        /// ファイルパスと最終更新日時をキーにしたメモリキャッシュから読み込む
        /// </summary>
        /// <param name="filePath">PDFファイルのパス</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>PDFファイルのバイト配列</returns>
        /// <remarks><see cref="MaxCacheableFileSizeBytes"/>を超えるPDFはキャッシュせず、都度ディスクから読み込む</remarks>
        private async Task<byte[]> GetFileBytesAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException("PDFファイルが見つかりません。", filePath);
            }

            DateTime lastWrite = fileInfo.LastWriteTimeUtc;
            long fileSize = fileInfo.Length;
            bool canCache = fileSize <= MaxCacheableFileSizeBytes;

            lock (_cacheLock)
            {
                if (canCache
                    && _cachedFilePath == filePath
                    && _cachedFileLastWrite == lastWrite
                    && _cachedFileBytes != null)
                {
                    return _cachedFileBytes;
                }
            }

            byte[] fileBytes = await Task.Run(() => File.ReadAllBytes(filePath));
            ValidatePdfHeader(fileBytes);

            lock (_cacheLock)
            {
                if (canCache)
                {
                    _cachedFilePath = filePath;
                    _cachedFileBytes = fileBytes;
                    _cachedFileLastWrite = lastWrite;
                }
                else
                {
                    InvalidateCacheUnsafe();
                }
            }

            return fileBytes;
        }

        /// <summary>メモリキャッシュを破棄する</summary>
        private void InvalidateCacheUnsafe()
        {
            _cachedFilePath = null;
            _cachedFileBytes = null;
            _cachedFileLastWrite = default;
        }

        /// <summary>
        /// 指定ページをスケーリング済みビットマップとして描画する
        /// </summary>
        /// <param name="docReader">DocReader</param>
        /// <param name="pageIndex">ページインデックス</param>
        /// <param name="mode">解像度の指定方法</param>
        /// <param name="value"><paramref name="mode"/>に対応する数値(幅・高さ・DPI)</param>
        /// <param name="preserveTransparency"><c>true</c>の場合は透明度を保持する</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>スケーリング済みビットマップ</returns>
        /// <exception cref="OperationCanceledException">キャンセル要求が発生した場合</exception>
        private static BitmapSource RenderScaledPage(IDocReader docReader, int pageIndex, ResolutionMode mode, double value, bool preserveTransparency, CancellationToken cancellationToken)
        {
            using (var pageReader = docReader.GetPageReader(pageIndex))
            {
                cancellationToken.ThrowIfCancellationRequested();
                BitmapSource source = CreateBitmapFromPageReader(pageReader);
                BitmapSource scaled = ScaleBitmap(source, mode, value);
                BitmapSource processed = ImageBitmapHelper.ApplyTransparency(scaled, preserveTransparency);
                if (processed.CanFreeze)
                {
                    processed.Freeze();
                }

                return processed;
            }
        }

        /// <summary>
        /// <see cref="IPageReader"/>のピクセルデータからビットマップを生成する
        /// </summary>
        /// <param name="pageReader">PageReader</param>
        /// <returns>ビットマップ</returns>
        private static BitmapSource CreateBitmapFromPageReader(IPageReader pageReader)
        {
            int width = pageReader.GetPageWidth();
            int height = pageReader.GetPageHeight();
            byte[] imageBytes = pageReader.GetImage();

            // Docnet.CoreはBGRA32形式でピクセルデータを返す(stride = width * 4 bytes)
            var source = BitmapSource.Create(
                width,
                height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                imageBytes,
                width * 4);
            source.Freeze();
            return source;
        }

        /// <summary>
        /// 指定ページを画像ファイルとして保存する
        /// </summary>
        /// <param name="docReader">DocReader</param>
        /// <param name="pageIndex">ページインデックス</param>
        /// <param name="folderPath">保存先フォルダーのパス</param>
        /// <param name="mode">解像度の指定方法</param>
        /// <param name="value"><paramref name="mode"/>に対応する数値(幅・高さ・DPI)</param>
        /// <param name="format">出力画像形式</param>
        /// <param name="preserveTransparency"><c>true</c>の場合は透明度を保持する</param>
        private static void SavePageToFile(IDocReader docReader, int pageIndex, string folderPath, ResolutionMode mode, double value, OutputImageFormat format, bool preserveTransparency)
        {
            using (var pageReader = docReader.GetPageReader(pageIndex))
            {
                BitmapSource source = CreateBitmapFromPageReader(pageReader);
                BitmapSource scaled = ScaleBitmap(source, mode, value);
                bool effectivePreserveTransparency = preserveTransparency && ImageBitmapHelper.SupportsTransparency(format);
                BitmapSource processed = ImageBitmapHelper.ApplyTransparency(scaled, effectivePreserveTransparency);
                string extension = ImageBitmapHelper.GetFileExtension(format);
                string outputPath = Path.Combine(folderPath, $"page_{pageIndex + 1}{extension}");
                ImageBitmapHelper.SaveToFile(processed, outputPath, format);
            }
        }

        /// <summary>
        /// ファイルの先頭4バイトがPDFマジックナンバー(<c>%PDF</c>)であることを検証する
        /// </summary>
        /// <param name="fileBytes">PDFファイルのバイト配列</param>
        /// <exception cref="InvalidDataException">PDFファイルではない場合</exception>
        private static void ValidatePdfHeader(byte[] fileBytes)
        {
            if (fileBytes == null || fileBytes.Length < 4)
            {
                throw new InvalidDataException("PDFファイルではありません。ファイルが破損している可能性があります。");
            }

            if (fileBytes[0] != '%'
                || fileBytes[1] != 'P'
                || fileBytes[2] != 'D'
                || fileBytes[3] != 'F')
            {
                throw new InvalidDataException("PDFファイルではありません。ファイルが破損している可能性があります。");
            }
        }

        /// <summary>
        /// <see cref="ResolutionMode"/>に基づいて<paramref name="source"/>をスケーリングする
        /// </summary>
        /// <param name="source">元ビットマップ</param>
        /// <param name="mode">解像度の指定方法</param>
        /// <param name="value"><paramref name="mode"/>に対応する数値(幅・高さ・DPI)</param>
        /// <returns>スケーリング済みビットマップ</returns>
        private static BitmapSource ScaleBitmap(BitmapSource source, ResolutionMode mode, double value)
        {
            if (mode == ResolutionMode.Default || value <= 0)
            {
                return source;
            }

            double targetWidth = source.PixelWidth;
            double targetHeight = source.PixelHeight;
            double aspectRatio = (double)source.PixelWidth / source.PixelHeight;

            if (mode == ResolutionMode.Width)
            {
                targetWidth = value;
                targetHeight = value / aspectRatio;
            }
            else if (mode == ResolutionMode.Height)
            {
                targetHeight = value;
                targetWidth = value * aspectRatio;
            }
            else if (mode == ResolutionMode.Dpi)
            {
                double scale = value / source.DpiX;
                targetWidth = source.PixelWidth * scale;
                targetHeight = source.PixelHeight * scale;
            }

            var scaled = new TransformedBitmap(source, new ScaleTransform(targetWidth / source.PixelWidth, targetHeight / source.PixelHeight));
            if (scaled.CanFreeze)
            {
                scaled.Freeze();
            }

            return scaled;
        }
    }
}
