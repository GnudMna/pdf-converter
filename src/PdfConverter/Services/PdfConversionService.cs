using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using PdfConverter.Models;

namespace PdfConverter.Services
{
    /// <summary>
    /// PDFium (PDFiumCore) を使用して PDF ページを画像に変換・保存するサービス
    /// </summary>
    /// <remarks>
    /// 同一ファイルへの連続アクセス時のディスク I/O を削減するため、
    /// 一定サイズ以下の PDF のみメモリキャッシュする<br/>
    /// 並列保存時はワーカーごとに PDFium の文書を開くため、
    /// ファイルサイズと描画サイズに応じて並列度を制限しメモリピークを抑える<br/>
    /// 描画サイズが <see cref="PdfiumDocument.MaxRenderPixels"/> を超えるページがある場合は、書き出しを始める前に中断する<br/>
    /// PDFium の呼び出しは <see cref="PdfiumDocument"/> がグローバルロックで直列化するため、
    /// 並列化で短縮されるのはビットマップ生成・画像エンコード・ファイル書き込みのみ<br/>
    /// PDF 解析やファイル反映が UI スレッドで実行されないよう、内部の await はすべて ConfigureAwait(false) とする
    /// </remarks>
    public class PdfConversionService : IPdfConversionService
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>メモリキャッシュの上限 (バイト)</summary>
        private const long MaxCacheableFileSizeBytes = 64L * 1024 * 1024;

        /// <summary>保存処理の並列ワーカー数の上限</summary>
        private const int MaxSaveParallelism = 4;

        /// <summary>フル並列を許可する PDF サイズの上限 (バイト)</summary>
        private const long SmallPdfThresholdBytes = 8L * 1024 * 1024;

        /// <summary>並列度を 2 に制限する PDF サイズの上限 (バイト)</summary>
        private const long MediumPdfThresholdBytes = 32L * 1024 * 1024;

        /// <summary>64 ビットプロセスで保存時に全ワーカーが同時に描画してよい画素数の合計</summary>
        /// <remarks>描画中は 1 ページにつき BGRA32 のビットマップが複数同時に存在するため、メモリピークは画素数の 10 数倍 (バイト) になる</remarks>
        private const long SaveRenderPixelBudgetX64 = 200_000_000;

        /// <summary>32 ビットプロセスで保存時に全ワーカーが同時に描画してよい画素数の合計</summary>
        private const long SaveRenderPixelBudgetX86 = 100_000_000;

        /// <summary>PDFium が scale 1.0 で描画するネイティブ DPI</summary>
        private const double PdfiumNativeDpi = 72.0;

        /// <summary>PDF ヘッダー (<c>%PDF</c>) を探索するファイル先頭からの最大オフセット (バイト)</summary>
        /// <remarks>PDF 仕様および PDFium と同じく、先頭 1024 バイト以内のヘッダーを許容する</remarks>
        private const int MaxPdfHeaderOffset = 1024;

        /// <summary>キャッシュの読み書きを保護するロックオブジェクト</summary>
        private readonly object _cacheLock = new object();

        /// <summary>現在キャッシュされているファイルのパス</summary>
        private string? _cachedFilePath;

        /// <summary>キャッシュされたファイルの生バイト列</summary>
        private byte[]? _cachedFileBytes;

        /// <summary>キャッシュ取得時点のファイル最終更新日時 (UTC)</summary>
        private DateTime _cachedFileLastWrite;


        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>実行中のプロセスで保存時に全ワーカーが同時に描画してよい画素数の合計</summary>
        private static long SaveRenderPixelBudget => Environment.Is64BitProcess ? SaveRenderPixelBudgetX64 : SaveRenderPixelBudgetX86;


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        public async Task<BitmapSource> ConvertPdfPageToImageAsync(string filePath, int pageIndex, ResolutionMode mode = ResolutionMode.Default, double value = 0, bool preserveTransparency = true, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("PDF ファイルが見つかりません。", filePath);
            }

            byte[] fileBytes = await GetFileBytesAsync(filePath, cancellationToken).ConfigureAwait(false);

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (var document = new PdfiumDocument(fileBytes))
                {
                    if (pageIndex < 0 || pageIndex >= document.PageCount)
                    {
                        throw new ArgumentOutOfRangeException(nameof(pageIndex), "ページインデックスが範囲外です。");
                    }

                    double renderScale = ComputeRenderScale(document, pageIndex, mode, value);
                    return RenderPage(document, pageIndex, renderScale, preserveTransparency, cancellationToken);
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<int> GetPdfPageCountAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("PDF ファイルが見つかりません。", filePath);
            }

            byte[] fileBytes = await GetFileBytesAsync(filePath, cancellationToken).ConfigureAwait(false);

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (var document = new PdfiumDocument(fileBytes))
                {
                    return document.PageCount;
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task SavePdfPagesToImagesAsync(string filePath, IEnumerable<int>? pageIndexes, string folderPath, bool saveAllPages, ResolutionMode mode = ResolutionMode.Default, double value = 0, OutputImageFormat format = OutputImageFormat.Png, bool preserveTransparency = true, IProgress<SaveProgressReport>? progress = null, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("PDF ファイルが見つかりません。", filePath);
            }

            if (string.IsNullOrWhiteSpace(folderPath))
            {
                throw new ArgumentException("保存先フォルダーのパスが指定されていません。", nameof(folderPath));
            }

            byte[] fileBytes = await GetFileBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            // 幅・高さ指定ではページサイズが混在する PDF でも各ページが指定サイズになるよう、ページごとに倍率を求める
            // 描画サイズが上限を超えるページがあれば、何も書き出さないうちに中断する
            int pageCount;
            List<int> pagesToSave;
            Dictionary<int, double> renderScaleByPage;
            long maxPagePixels = 0;
            using (var document = new PdfiumDocument(fileBytes))
            {
                pageCount = document.PageCount;
                pagesToSave = ResolvePagesToSave(pageIndexes, saveAllPages, pageCount);
                renderScaleByPage = pagesToSave.ToDictionary(
                    pageIndex => pageIndex,
                    pageIndex => ComputeRenderScale(document, pageIndex, mode, value));

                foreach (int pageIndex in pagesToSave)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    (int width, int height) = document.GetRenderSize(pageIndex, renderScaleByPage[pageIndex]);
                    maxPagePixels = Math.Max(maxPagePixels, (long)width * height);
                }
            }

            int total = pagesToSave.Count;
            int completedCount = 0;
            object progressLock = new object();

            // 失敗・キャンセル時に保存先へ部分出力を残さないため、一時ディレクトリへ書き出してから一括反映する
            string stagingDirectory = PdfConverterTempDirectory.CreateTempWorkDirectory();
            try
            {
                // ワーカー内では 1 つの文書を担当ページすべてで再利用し、ページごとの読み込みコストを削減する
                // 大容量 PDF では文書ごとのネイティブメモリが積み上がるため、ファイルサイズに応じて並列度を抑える
                // 高解像度ではページごとのビットマップが大きくなるため、描画サイズに応じても並列度を抑える
                int maxParallelism = ResolveSaveParallelism(Environment.ProcessorCount, fileBytes.Length, maxPagePixels, SaveRenderPixelBudget);
                List<List<int>> partitions = PartitionPages(pagesToSave, maxParallelism);

                var tasks = partitions
                    .Where(partition => partition.Count > 0)
                    .Select(partition => Task.Run(() =>
                    {
                        using (var document = new PdfiumDocument(fileBytes))
                        {
                            foreach (int pageIndex in partition)
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                SavePageToFile(document, pageIndex, renderScaleByPage[pageIndex], pageCount, stagingDirectory, format, preserveTransparency);
                                int completed = Interlocked.Increment(ref completedCount);
                                var report = new SaveProgressReport(completed * 100.0 / total, $"保存中... {completed}/{total} ページ");
                                lock (progressLock)
                                {
                                    progress?.Report(report);
                                }
                            }
                        }
                    }, cancellationToken));

                await Task.WhenAll(tasks).ConfigureAwait(false);

                // 全ページの書き出し後に要求されたキャンセルでも、保存先へ反映せずに中断する
                cancellationToken.ThrowIfCancellationRequested();

                CommitStagedFiles(stagingDirectory, folderPath, pagesToSave, pageCount, format);

                if (progress != null)
                {
                    progress.Report(new SaveProgressReport(100.0, $"保存中... {total}/{total} ページ"));
                }
            }
            finally
            {
                PdfConverterTempDirectory.TryDeleteDirectory(stagingDirectory);
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
        /// <param name="pageCount">PDF のページ数</param>
        /// <returns>保存対象のページインデックス一覧</returns>
        /// <exception cref="ArgumentException">有効なページインデックス一覧または"全ページを保存"を選択してください</exception>
        /// <exception cref="ArgumentOutOfRangeException">範囲外のページインデックスが含まれている場合</exception>
        private static List<int> ResolvePagesToSave(IEnumerable<int>? pageIndexes, bool saveAllPages, int pageCount)
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
                throw new ArgumentOutOfRangeException(nameof(pageIndexes), "範囲外のページインデックスが含まれています。PDF のページ数内で指定してください。");
            }

            return pagesToSave;
        }

        /// <summary>
        /// 保存処理の並列ワーカー数を CPU コア数・PDF サイズ・描画サイズから決定する
        /// </summary>
        /// <param name="processorCount">利用可能な論理プロセッサ数</param>
        /// <param name="fileSizeBytes">PDF ファイルサイズ (バイト)</param>
        /// <param name="maxPagePixels">保存対象ページのうち最大の描画画素数</param>
        /// <param name="pixelBudget">全ワーカーで同時に描画してよい画素数の合計</param>
        /// <returns>1以上の並列ワーカー数</returns>
        internal static int ResolveSaveParallelism(int processorCount, long fileSizeBytes, long maxPagePixels, long pixelBudget)
        {
            int cpuBound = Math.Max(1, Math.Min(processorCount, MaxSaveParallelism));

            int fileSizeBound;
            if (fileSizeBytes <= SmallPdfThresholdBytes)
            {
                fileSizeBound = cpuBound;
            }
            else if (fileSizeBytes <= MediumPdfThresholdBytes)
            {
                fileSizeBound = Math.Min(cpuBound, 2);
            }
            else
            {
                fileSizeBound = 1;
            }

            if (maxPagePixels <= 0)
            {
                return fileSizeBound;
            }

            long pixelBound = Math.Max(1, pixelBudget / maxPagePixels);
            return (int)Math.Min(fileSizeBound, pixelBound);
        }

        /// <summary>
        /// ページ一覧を並列ワーカー数に応じて分割する
        /// </summary>
        /// <param name="pages">0始まりのページインデックス一覧</param>
        /// <param name="partitionCount">分割するパーティション数</param>
        /// <returns>分割されたページインデックスのリスト</returns>
        private static List<List<int>> PartitionPages(List<int> pages, int partitionCount)
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
        /// <param name="filePath">PDF ファイルのパス</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>PDF ファイルのバイト配列</returns>
        /// <remarks><see cref="MaxCacheableFileSizeBytes"/> を超える PDF はキャッシュせず、都度ディスクから読み込む</remarks>
        private async Task<byte[]> GetFileBytesAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException("PDF ファイルが見つかりません。", filePath);
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

            byte[] fileBytes = await Task.Run(() => File.ReadAllBytes(filePath)).ConfigureAwait(false);
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
        /// 解像度モードと値から PDFium のレンダリングスケールを計算する
        /// </summary>
        /// <param name="document">PDF 文書</param>
        /// <param name="samplePageIndex">Width/Height モードで自然サイズを取得するページインデックス</param>
        /// <param name="mode">解像度の指定方法</param>
        /// <param name="value">解像度の値</param>
        /// <returns>PDFium レンダリングスケール (1.0 = 72 DPI)</returns>
        private static double ComputeRenderScale(PdfiumDocument document, int samplePageIndex, ResolutionMode mode, double value)
        {
            if (mode == ResolutionMode.Default || value <= 0)
            {
                return 1.0;
            }

            if (mode == ResolutionMode.Dpi)
            {
                return value / PdfiumNativeDpi;
            }

            (int naturalWidth, int naturalHeight) = document.GetPagePixelSize(samplePageIndex, 1.0);

            if (mode == ResolutionMode.Width)
            {
                return value / naturalWidth;
            }

            return value / naturalHeight;
        }

        /// <summary>
        /// 指定ページをビットマップとして描画する
        /// </summary>
        /// <param name="document">PDF 文書</param>
        /// <param name="pageIndex">ページインデックス</param>
        /// <param name="renderScale">描画倍率</param>
        /// <param name="preserveTransparency"><c>true</c>の場合は透明度を保持する</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>ビットマップ</returns>
        /// <exception cref="OperationCanceledException">キャンセル要求が発生した場合</exception>
        private static BitmapSource RenderPage(PdfiumDocument document, int pageIndex, double renderScale, bool preserveTransparency, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BitmapSource source = document.RenderPage(pageIndex, renderScale);
            BitmapSource processed = ImageBitmapHelper.ApplyTransparency(source, preserveTransparency);
            if (processed.CanFreeze)
            {
                processed.Freeze();
            }

            return processed;
        }

        /// <summary>
        /// 指定ページを画像ファイルとして保存する
        /// </summary>
        /// <param name="document">PDF 文書</param>
        /// <param name="pageIndex">ページインデックス</param>
        /// <param name="renderScale">描画倍率</param>
        /// <param name="pageCount">PDF の総ページ数 (ファイル名のゼロ埋め桁数に使用)</param>
        /// <param name="folderPath">保存先フォルダーのパス</param>
        /// <param name="format">出力画像形式</param>
        /// <param name="preserveTransparency"><c>true</c>の場合は透明度を保持する</param>
        private static void SavePageToFile(PdfiumDocument document, int pageIndex, double renderScale, int pageCount, string folderPath, OutputImageFormat format, bool preserveTransparency)
        {
            BitmapSource source = document.RenderPage(pageIndex, renderScale);
            bool effectivePreserveTransparency = preserveTransparency && ImageBitmapHelper.SupportsTransparency(format);
            BitmapSource processed = ImageBitmapHelper.ApplyTransparency(source, effectivePreserveTransparency);
            string outputPath = Path.Combine(folderPath, PageImageFileNameHelper.GetFileName(pageIndex, pageCount, format));
            ImageBitmapHelper.SaveToFile(processed, outputPath, format);
        }

        /// <summary>
        /// ステージング済みの画像ファイルを保存先フォルダーへ反映する
        /// </summary>
        /// <param name="stagingDirectory">ステージングディレクトリの絶対パス</param>
        /// <param name="folderPath">保存先フォルダーの絶対パス</param>
        /// <param name="pagesToSave">保存対象のページインデックス一覧</param>
        /// <param name="pageCount">PDF の総ページ数 (ファイル名のゼロ埋め桁数に使用)</param>
        /// <param name="format">出力画像形式</param>
        /// <exception cref="FileNotFoundException">ステージング済みファイルが見つからない場合</exception>
        private static void CommitStagedFiles(
            string stagingDirectory,
            string folderPath,
            IReadOnlyList<int> pagesToSave,
            int pageCount,
            OutputImageFormat format)
        {
            Directory.CreateDirectory(folderPath);

            foreach (int pageIndex in pagesToSave)
            {
                string fileName = PageImageFileNameHelper.GetFileName(pageIndex, pageCount, format);
                string sourcePath = Path.Combine(stagingDirectory, fileName);
                if (!File.Exists(sourcePath))
                {
                    throw new FileNotFoundException(
                        $"ステージングされた出力ファイルが見つかりません: {fileName}",
                        sourcePath);
                }

                string destinationPath = Path.Combine(folderPath, fileName);
                ReplaceFile(sourcePath, destinationPath);
            }
        }

        /// <summary>
        /// ソースファイルで宛先ファイルを置き換える
        /// </summary>
        /// <param name="sourcePath">反映元ファイルの絶対パス</param>
        /// <param name="destinationPath">反映先ファイルの絶対パス</param>
        /// <remarks>
        /// 一時ディレクトリと保存先が異ボリュームでも動くよう、コピー後にソースを削除する
        /// </remarks>
        private static void ReplaceFile(string sourcePath, string destinationPath)
        {
            File.Copy(sourcePath, destinationPath, overwrite: true);
            File.Delete(sourcePath);
        }

        /// <summary>
        /// ファイル先頭 <see cref="MaxPdfHeaderOffset"/> バイト以内に PDF マジックナンバー (<c>%PDF</c>) があることを検証する
        /// </summary>
        /// <param name="fileBytes">PDF ファイルのバイト配列</param>
        /// <exception cref="InvalidDataException">PDF ファイルではない場合</exception>
        private static void ValidatePdfHeader(byte[] fileBytes)
        {
            if (FindPdfHeaderOffset(fileBytes) < 0)
            {
                throw new InvalidDataException("PDF ファイルではありません。ファイルが破損している可能性があります。");
            }
        }

        /// <summary>
        /// ファイル先頭 <see cref="MaxPdfHeaderOffset"/> バイト以内にある PDF マジックナンバー (<c>%PDF</c>) の位置を返す
        /// </summary>
        /// <param name="fileBytes">PDF ファイルのバイト配列</param>
        /// <returns>マジックナンバーの開始オフセット。見つからない場合は -1</returns>
        internal static int FindPdfHeaderOffset(byte[] fileBytes)
        {
            if (fileBytes == null)
            {
                return -1;
            }

            int lastOffset = Math.Min(MaxPdfHeaderOffset, fileBytes.Length - 4);
            for (int offset = 0; offset <= lastOffset; offset++)
            {
                if (fileBytes[offset] == '%'
                    && fileBytes[offset + 1] == 'P'
                    && fileBytes[offset + 2] == 'D'
                    && fileBytes[offset + 3] == 'F')
                {
                    return offset;
                }
            }

            return -1;
        }
    }
}
