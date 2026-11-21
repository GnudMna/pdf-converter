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
    /// PDF 解析やファイル反映が UI スレッドで実行されないよう、内部の await はすべて ConfigureAwait(false) とする<br/>
    /// ページ数取得とページ単位の描画 (プレビュー) では、直近に開いた PDFium の文書を開いたまま再利用し、ページ移動ごとの読み込み・解析を省く<br/>
    /// メモリキャッシュの上限を超える PDF は、プレビュー・保存ともファイル全体を読み込まず、PDFium が必要とした範囲だけをファイルから読む
    /// </remarks>
    public sealed class PdfConversionService : IPdfConversionService, IDisposable
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

        /// <summary>PDF ヘッダー (<c>%PDF</c>) を探索するファイル先頭からの最大オフセット (バイト)</summary>
        /// <remarks>PDF 仕様および PDFium と同じく、先頭 1024 バイト以内のヘッダーを許容する</remarks>
        private const int MaxPdfHeaderOffset = 1024;

        /// <summary>保存先フォルダー内に作るステージングディレクトリ名の接頭辞</summary>
        private const string StagingDirectoryPrefix = ".pdfconverter-staging-";

        /// <summary>強制終了などで残ったステージングディレクトリを削除対象とする経過時間</summary>
        private static readonly TimeSpan StaleStagingDirectoryAge = PdfConverterTempDirectory.DefaultStaleAge;

        /// <summary>キャッシュの読み書きを保護するロックオブジェクト</summary>
        private readonly object _cacheLock = new object();

        /// <summary>パスワードを設定したファイルのパス (<see cref="_cacheLock"/> で保護する)</summary>
        private string? _passwordFilePath;

        /// <summary>設定されたパスワード (<see cref="_cacheLock"/> で保護する)</summary>
        private string? _password;

        /// <summary>現在キャッシュされているファイルのパス</summary>
        private string? _cachedFilePath;

        /// <summary>キャッシュされたファイルの生バイト列</summary>
        private byte[]? _cachedFileBytes;

        /// <summary>キャッシュ取得時点のファイル最終更新日時 (UTC)</summary>
        private DateTime _cachedFileLastWrite;

        /// <summary>開いたまま再利用する PDFium の文書 (<see cref="_cacheLock"/> で保護する)</summary>
        private CachedDocument? _cachedDocument;

        /// <summary>破棄済みかどうか</summary>
        private bool _disposed;


        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>実行中のプロセスで保存時に全ワーカーが同時に描画してよい画素数の合計</summary>
        private static long SaveRenderPixelBudget => Environment.Is64BitProcess ? SaveRenderPixelBudgetX64 : SaveRenderPixelBudgetX86;


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        public void SetPdfPassword(string filePath, string? password)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            lock (_cacheLock)
            {
                _passwordFilePath = password == null ? null : filePath;
                _password = password;
            }
        }

        /// <inheritdoc/>
        public async Task<BitmapSource> ConvertPdfPageToImageAsync(string filePath, int pageIndex, ResolutionMode mode = ResolutionMode.Default, double value = 0, bool preserveTransparency = true, CancellationToken cancellationToken = default)
        {
            CachedDocument cachedDocument = await AcquireDocumentAsync(filePath, cancellationToken).ConfigureAwait(false);
            try
            {
                return await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    PdfiumDocument document = cachedDocument.Document;
                    ValidatePageIndex(document, pageIndex);
                    double renderScale = ComputeRenderScale(document, pageIndex, mode, value);
                    return RenderPage(document, pageIndex, renderScale, preserveTransparency, cancellationToken);
                }, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                cachedDocument.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<PdfPagePreview> RenderPdfPagePreviewAsync(string filePath, int pageIndex, ResolutionMode mode, double value, bool preserveTransparency, int maxPixelWidth, int maxPixelHeight, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maxPixelWidth, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(maxPixelHeight, 1);

            CachedDocument cachedDocument = await AcquireDocumentAsync(filePath, cancellationToken).ConfigureAwait(false);
            try
            {
                return await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    PdfiumDocument document = cachedDocument.Document;
                    ValidatePageIndex(document, pageIndex);
                    double renderScale = ComputeRenderScale(document, pageIndex, mode, value);

                    // 出力解像度で描画できないページは、縮小すれば描画できても保存時と同じく失敗させる
                    (int outputWidth, int outputHeight) = document.GetRenderSize(pageIndex, renderScale);
                    (double pageWidth, double pageHeight) = document.GetPageSize(pageIndex);
                    double previewScale = ComputePreviewScale(pageWidth, pageHeight, renderScale, maxPixelWidth, maxPixelHeight);

                    BitmapSource image = RenderPage(document, pageIndex, previewScale, preserveTransparency, cancellationToken);
                    return new PdfPagePreview(image, outputWidth, outputHeight);
                }, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                cachedDocument.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<int> GetPdfPageCountAsync(string filePath, CancellationToken cancellationToken = default)
        {
            CachedDocument cachedDocument = await AcquireDocumentAsync(filePath, cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return cachedDocument.Document.PageCount;
            }
            finally
            {
                cachedDocument.Release();
            }
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

            // メモリキャッシュの上限を超える PDF は、プレビューと同じくファイル全体を読み込まずに開く
            long fileSize = new FileInfo(filePath).Length;
            string? password = GetPassword(filePath);
            Func<PdfiumDocument> openDocument;
            PdfiumDocument firstDocument;
            if (fileSize <= MaxCacheableFileSizeBytes)
            {
                byte[] fileBytes = await GetFileBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
                fileSize = fileBytes.Length;
                openDocument = () => new PdfiumDocument(fileBytes, password);
                firstDocument = await Task.Run(openDocument, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                openDocument = () => PdfiumDocument.OpenFile(filePath, password);
                firstDocument = await Task.Run(() => OpenLargeFile(filePath, password), cancellationToken).ConfigureAwait(false);
            }

            // 幅・高さ指定ではページサイズが混在する PDF でも各ページが指定サイズになるよう、ページごとに倍率を求める
            // 描画サイズが上限を超えるページがあれば、何も書き出さないうちに中断する
            int pageCount;
            List<int> pagesToSave;
            Dictionary<int, double> renderScaleByPage;
            long maxPagePixels = 0;
            using (PdfiumDocument document = firstDocument)
            {
                cancellationToken.ThrowIfCancellationRequested();

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

            // 失敗・キャンセル時に保存先へ部分出力を残さないため、保存先フォルダー内のステージングディレクトリへ書き出してから一括反映する
            // %TEMP% を経由せず、書き出しは 1 回で済み、反映は同一ボリューム内のリネームだけで完結する
            string fullFolderPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath));
            string? createdRootPath = FindTopmostMissingDirectory(fullFolderPath);
            string stagingDirectory = Path.Combine(fullFolderPath, StagingDirectoryPrefix + Guid.NewGuid().ToString("N"));
            bool committed = false;
            try
            {
                // 前回の保存が強制終了されて残ったステージングディレクトリを片付ける
                TryDeleteStaleStagingDirectories(fullFolderPath, StaleStagingDirectoryAge);

                // 強制終了で残った場合に利用者の目に触れないよう、隠しフォルダーにする
                Directory.CreateDirectory(stagingDirectory);
                TryHideDirectory(stagingDirectory);

                // ワーカー内では 1 つの文書を担当ページすべてで再利用し、ページごとの読み込みコストを削減する
                // 大容量 PDF では文書ごとのネイティブメモリが積み上がるため、ファイルサイズに応じて並列度を抑える
                // 高解像度ではページごとのビットマップが大きくなるため、描画サイズに応じても並列度を抑える
                int maxParallelism = ResolveSaveParallelism(Environment.ProcessorCount, fileSize, maxPagePixels, SaveRenderPixelBudget);
                List<List<int>> partitions = PartitionPages(pagesToSave, maxParallelism);

                // いずれかのワーカーが失敗したら残りのワーカーも打ち切り、結果が出ない描画を続けないようにする
                using (CancellationTokenSource workerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    CancellationToken workerToken = workerCts.Token;

                    var tasks = partitions
                        .Where(partition => partition.Count > 0)
                        .Select(partition => Task.Run(() =>
                        {
                            try
                            {
                                using (PdfiumDocument document = openDocument())
                                {
                                    foreach (int pageIndex in partition)
                                    {
                                        workerToken.ThrowIfCancellationRequested();

                                        SavePageToFile(document, pageIndex, renderScaleByPage[pageIndex], pageCount, stagingDirectory, format, preserveTransparency);

                                        // 件数の加算から通知までを同じロックで行い、並列ワーカー間で進捗が逆戻りしないようにする
                                        lock (progressLock)
                                        {
                                            completedCount++;
                                            progress?.Report(new SaveProgressReport(completedCount * 100.0 / total, $"保存中... {completedCount}/{total} ページ"));
                                        }
                                    }
                                }
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                workerCts.Cancel();
                                throw;
                            }
                        }, workerToken))
                        .ToList();

                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }

                // 全ページの書き出し後に要求されたキャンセルでも、保存先へ反映せずに中断する
                cancellationToken.ThrowIfCancellationRequested();

                CommitStagedFiles(stagingDirectory, fullFolderPath, pagesToSave, pageCount, format);
                committed = true;

                if (progress != null)
                {
                    progress.Report(new SaveProgressReport(100.0, $"保存中... {total}/{total} ページ"));
                }
            }
            finally
            {
                PdfConverterTempDirectory.TryDeleteDirectory(stagingDirectory);

                // 失敗・キャンセル時は、この保存で新規に作ったフォルダーも空であれば残さない
                if (!committed && createdRootPath != null)
                {
                    TryDeleteEmptyDirectories(fullFolderPath, createdRootPath);
                }
            }
        }

        /// <summary>
        /// 再利用のため開いている文書とメモリキャッシュを破棄する
        /// </summary>
        /// <remarks>使用中の文書は、使用が終わった時点で閉じる</remarks>
        public void Dispose()
        {
            CachedDocument? cachedDocument;
            lock (_cacheLock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _passwordFilePath = null;
                _password = null;
                cachedDocument = _cachedDocument;
                _cachedDocument = null;
                InvalidateCacheUnsafe();
            }

            cachedDocument?.Evict();
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// 指定ファイルに設定されたパスワードを取得する
        /// </summary>
        /// <param name="filePath">PDF ファイルのパス</param>
        /// <returns>設定されたパスワード。設定されていない場合は <c>null</c></returns>
        /// <remarks>パスは大文字小文字を区別せずに比較する。別のファイルが指定された場合は、保持しているパスワードを破棄する</remarks>
        private string? GetPassword(string filePath)
        {
            lock (_cacheLock)
            {
                if (_passwordFilePath == null)
                {
                    return null;
                }

                if (!string.Equals(_passwordFilePath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    _passwordFilePath = null;
                    _password = null;
                    return null;
                }

                return _password;
            }
        }

        /// <summary>
        /// 保存対象ページ一覧を解決する
        /// </summary>
        /// <param name="pageIndexes">保存対象のページインデックス一覧</param>
        /// <param name="saveAllPages"><c>true</c> の場合は全ページを保存する</param>
        /// <param name="pageCount">PDF のページ数</param>
        /// <returns>保存対象のページインデックス一覧</returns>
        /// <exception cref="ArgumentException">全ページ保存が指定されておらず、ページインデックス一覧が <c>null</c> または空の場合</exception>
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
        /// <returns>1 以上の並列ワーカー数</returns>
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
        /// <param name="pages">0 始まりのページインデックス一覧</param>
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
        /// <remarks>ファイルが <see cref="MaxCacheableFileSizeBytes"/> を超える場合はキャッシュせず、ディスクから読み込んだバイト列をそのまま返す</remarks>
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
                    && string.Equals(_cachedFilePath, filePath, StringComparison.OrdinalIgnoreCase)
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
        /// 指定ファイルの PDFium の文書を、開いたままの文書があれば再利用し、なければ開いて取得する
        /// </summary>
        /// <remarks>
        /// ファイルパス・最終更新日時・ファイルサイズが一致する場合のみ再利用し、一致しない場合は開き直して古い文書を手放す<br/>
        /// <see cref="MaxCacheableFileSizeBytes"/> 以下の PDF はメモリキャッシュしたバイト列から、超える PDF はファイルから必要な範囲だけを読んで開く<br/>
        /// 呼び出し側は使用後に必ず <see cref="CachedDocument.Release"/> を呼ぶこと
        /// </remarks>
        /// <param name="filePath">PDF ファイルのパス</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>参照を 1 つ取得済みの文書</returns>
        /// <exception cref="FileNotFoundException">ファイルが存在しない場合</exception>
        /// <exception cref="PdfPasswordRequiredException">パスワードが必要、または設定したパスワードが正しくない場合</exception>
        /// <exception cref="InvalidDataException">PDF ファイルではない、または開けない場合</exception>
        /// <exception cref="ObjectDisposedException">破棄済みの場合</exception>
        private async Task<CachedDocument> AcquireDocumentAsync(string filePath, CancellationToken cancellationToken)
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException("PDF ファイルが見つかりません。", filePath);
            }

            DateTime lastWrite = fileInfo.LastWriteTimeUtc;
            long fileSize = fileInfo.Length;

            lock (_cacheLock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_cachedDocument != null
                    && _cachedDocument.Matches(filePath, lastWrite, fileSize)
                    && _cachedDocument.TryAddReference())
                {
                    return _cachedDocument;
                }
            }

            string? password = GetPassword(filePath);
            PdfiumDocument document;
            if (fileSize <= MaxCacheableFileSizeBytes)
            {
                byte[] fileBytes = await GetFileBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
                document = await Task.Run(() => new PdfiumDocument(fileBytes, password), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                document = await Task.Run(() => OpenLargeFile(filePath, password), cancellationToken).ConfigureAwait(false);
            }

            var cachedDocument = new CachedDocument(document, filePath, lastWrite, fileSize);
            CachedDocument? previousDocument;
            lock (_cacheLock)
            {
                if (_disposed)
                {
                    document.Dispose();
                    throw new ObjectDisposedException(nameof(PdfConversionService));
                }

                previousDocument = _cachedDocument;
                _cachedDocument = cachedDocument;
            }

            previousDocument?.Evict();
            return cachedDocument;
        }

        /// <summary>
        /// メモリキャッシュの上限を超える PDF を、ヘッダーを検証したうえでファイル全体を読み込まずに開く
        /// </summary>
        /// <param name="filePath">PDF ファイルのパス</param>
        /// <param name="password">PDF を開くパスワード (パスワードなしの場合は <c>null</c>)</param>
        /// <returns>開いた文書</returns>
        /// <exception cref="InvalidDataException">PDF ファイルではない、または開けない場合</exception>
        private static PdfiumDocument OpenLargeFile(string filePath, string? password)
        {
            byte[] header;
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                header = new byte[(int)Math.Min(stream.Length, MaxPdfHeaderOffset + 4)];
                stream.ReadExactly(header);
            }

            ValidatePdfHeader(header);
            return PdfiumDocument.OpenFile(filePath, password);
        }

        /// <summary>
        /// ページインデックスが文書の範囲内であることを検証する
        /// </summary>
        /// <param name="document">PDF 文書</param>
        /// <param name="pageIndex">ページインデックス</param>
        /// <exception cref="ArgumentOutOfRangeException">範囲外の場合</exception>
        private static void ValidatePageIndex(PdfiumDocument document, int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= document.PageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(pageIndex), "ページインデックスが範囲外です。");
            }
        }

        /// <summary>
        /// 出力解像度の描画倍率を上限に、ページが指定サイズに収まるプレビューの描画倍率を返す
        /// </summary>
        /// <remarks>
        /// 出力解像度で収まる場合は出力解像度のまま描画し、拡大はしない<br/>
        /// 極端に細長いページでも短辺が 1px 未満にならないよう、短辺が 1px になる倍率を下限とする
        /// </remarks>
        /// <param name="pageWidth">ページの幅 (ポイント)</param>
        /// <param name="pageHeight">ページの高さ (ポイント)</param>
        /// <param name="renderScale">出力解像度の描画倍率</param>
        /// <param name="maxPixelWidth">プレビュー画像の最大幅 (px)</param>
        /// <param name="maxPixelHeight">プレビュー画像の最大高さ (px)</param>
        /// <returns>プレビューの描画倍率 (1.0 = 72 DPI)</returns>
        internal static double ComputePreviewScale(double pageWidth, double pageHeight, double renderScale, int maxPixelWidth, int maxPixelHeight)
        {
            double fitRatio = Math.Min(maxPixelWidth / (pageWidth * renderScale), maxPixelHeight / (pageHeight * renderScale));

            // NaN (不正なページサイズ) も出力解像度のまま描画し、描画サイズの検証で失敗させる
            if (!(fitRatio < 1.0))
            {
                return renderScale;
            }

            double minimumScale = ScaleToReachLength(Math.Min(pageWidth, pageHeight), 1);
            return Math.Min(renderScale, Math.Max(renderScale * fitRatio, minimumScale));
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
                return value / PdfiumDocument.NativeDpi;
            }

            // 切り捨て前の実数サイズで倍率を求める (整数化すると A4 の 841.92pt などで出力が 1px ずれる)
            (double naturalWidth, double naturalHeight) = document.GetPageSize(samplePageIndex);
            double naturalLength = mode == ResolutionMode.Width ? naturalWidth : naturalHeight;

            return ScaleToReachLength(naturalLength, value);
        }

        /// <summary>
        /// 実数の長さに掛けて切り捨てたとき、ちょうど目標のピクセル数になる倍率を返す
        /// </summary>
        /// <remarks>
        /// 浮動小数点の丸め誤差で <c>naturalLength * scale</c> が目標をわずかに下回ると、切り捨てで 1px 小さくなる<br/>
        /// そのため下回る間は倍率を最小単位だけ引き上げる
        /// </remarks>
        /// <param name="naturalLength">倍率 1.0 での長さ (ポイント)</param>
        /// <param name="targetPixels">目標のピクセル数</param>
        /// <returns>PDFium レンダリングスケール (1.0 = 72 DPI)</returns>
        private static double ScaleToReachLength(double naturalLength, double targetPixels)
        {
            double scale = targetPixels / naturalLength;
            if (!double.IsFinite(scale))
            {
                return scale;
            }

            double floorTarget = Math.Floor(targetPixels);
            while (Math.Floor(naturalLength * scale) < floorTarget)
            {
                scale = Math.BitIncrement(scale);
            }

            return scale;
        }

        /// <summary>
        /// 指定ページをビットマップとして描画する
        /// </summary>
        /// <param name="document">PDF 文書</param>
        /// <param name="pageIndex">ページインデックス</param>
        /// <param name="renderScale">描画倍率</param>
        /// <param name="preserveTransparency"><c>true</c> の場合は透明度を保持する</param>
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
        /// <param name="preserveTransparency"><c>true</c> の場合は透明度を保持する</param>
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
        /// <param name="stagingDirectory">ステージングディレクトリの絶対パス (保存先フォルダー内にあること)</param>
        /// <param name="folderPath">保存先フォルダーの絶対パス</param>
        /// <param name="pagesToSave">保存対象のページインデックス一覧</param>
        /// <param name="pageCount">PDF の総ページ数 (ファイル名のゼロ埋め桁数に使用)</param>
        /// <param name="format">出力画像形式</param>
        /// <remarks>
        /// ステージングディレクトリは保存先フォルダー内にあるため、コピーせず同一ボリューム内のリネームだけで配置する<br/>
        /// 既存ファイルはステージングディレクトリへ退避してから置き換える<br/>
        /// いずれかのファイルで失敗した場合は、配置済みのファイルを削除して退避した既存ファイルを元に戻す<br/>
        /// 退避したファイルの削除はステージングディレクトリごと呼び出し元が行う
        /// </remarks>
        /// <exception cref="FileNotFoundException">ステージング済みファイルが見つからない場合</exception>
        private static void CommitStagedFiles(
            string stagingDirectory,
            string folderPath,
            List<int> pagesToSave,
            int pageCount,
            OutputImageFormat format)
        {
            var entries = new List<CommitEntry>(pagesToSave.Count);
            try
            {
                foreach (int pageIndex in pagesToSave)
                {
                    string fileName = PageImageFileNameHelper.GetFileName(pageIndex, pageCount, format);
                    string stagedPath = Path.Combine(stagingDirectory, fileName);
                    if (!File.Exists(stagedPath))
                    {
                        throw new FileNotFoundException(
                            $"ステージングされた出力ファイルが見つかりません: {fileName}",
                            stagedPath);
                    }

                    var entry = new CommitEntry(Path.Combine(folderPath, fileName), stagedPath, $"{stagedPath}.bak");
                    entries.Add(entry);
                    entry.Install();
                }
            }
            catch
            {
                for (int i = entries.Count - 1; i >= 0; i--)
                {
                    entries[i].Rollback();
                }

                throw;
            }
        }

        /// <summary>
        /// 保存先フォルダーに残っている古いステージングディレクトリを削除する (削除できなくても例外は送出しない)
        /// </summary>
        /// <param name="folderPath">保存先フォルダーの絶対パス</param>
        /// <param name="maxAge">最終更新からこの時間を超えたディレクトリを削除対象とする</param>
        /// <param name="referenceUtcNow">基準時刻 (テスト用。省略時は <see cref="DateTime.UtcNow"/>)</param>
        /// <remarks>
        /// 保存の途中でプロセスが強制終了されると、finally による削除が行われずに残る<br/>
        /// 名前が <see cref="StagingDirectoryPrefix"/> と GUID (32 桁の 16 進数) に一致するものだけを対象にし、
        /// 他の保存が使用中のものを消さないよう最近更新されたものとシンボリックリンクは残す
        /// </remarks>
        internal static void TryDeleteStaleStagingDirectories(string folderPath, TimeSpan maxAge, DateTime? referenceUtcNow = null)
        {
            try
            {
                if (!Directory.Exists(folderPath))
                {
                    return;
                }

                DateTime cutoff = (referenceUtcNow ?? DateTime.UtcNow) - maxAge;
                foreach (string directoryPath in Directory.EnumerateDirectories(folderPath, StagingDirectoryPrefix + "*"))
                {
                    string name = Path.GetFileName(directoryPath);
                    if (!IsStagingDirectoryName(name)
                        || (File.GetAttributes(directoryPath) & FileAttributes.ReparsePoint) != 0
                        || Directory.GetLastWriteTimeUtc(directoryPath) >= cutoff)
                    {
                        continue;
                    }

                    PdfConverterTempDirectory.TryDeleteDirectory(directoryPath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 後始末は最善努力とし、保存を妨げない
            }
        }

        /// <summary>
        /// ディレクトリ名がステージングディレクトリのものかどうかを判定する
        /// </summary>
        /// <param name="name">ディレクトリ名 (パスを含まない)</param>
        /// <returns>接頭辞の後ろが 32 桁の 16 進数の場合は <c>true</c></returns>
        private static bool IsStagingDirectoryName(string name)
        {
            return name.StartsWith(StagingDirectoryPrefix, StringComparison.Ordinal)
                && Guid.TryParseExact(name.AsSpan(StagingDirectoryPrefix.Length), "N", out _);
        }

        /// <summary>
        /// ディレクトリを隠しフォルダーにする (できなくても例外は送出しない)
        /// </summary>
        /// <param name="directoryPath">対象ディレクトリの絶対パス</param>
        private static void TryHideDirectory(string directoryPath)
        {
            try
            {
                File.SetAttributes(directoryPath, File.GetAttributes(directoryPath) | FileAttributes.Hidden);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 属性を設定できないファイルシステムでも保存は続ける
            }
        }

        /// <summary>
        /// 指定したフォルダーまでのパスのうち、まだ存在しない最上位のディレクトリを返す
        /// </summary>
        /// <param name="fullFolderPath">フォルダーの絶対パス</param>
        /// <returns>存在しない最上位のディレクトリの絶対パス。フォルダーが既に存在する場合は <c>null</c></returns>
        private static string? FindTopmostMissingDirectory(string fullFolderPath)
        {
            string? topmostMissing = null;
            for (string? current = fullFolderPath; current != null && !Directory.Exists(current); current = Path.GetDirectoryName(current))
            {
                topmostMissing = current;
            }

            return topmostMissing;
        }

        /// <summary>
        /// 保存先フォルダーから作成起点のディレクトリまで、空のディレクトリを内側から順に削除する (削除できなくても例外は送出しない)
        /// </summary>
        /// <param name="fullFolderPath">保存先フォルダーの絶対パス</param>
        /// <param name="createdRootPath">この保存で新規に作成したディレクトリのうち最上位のもの</param>
        private static void TryDeleteEmptyDirectories(string fullFolderPath, string createdRootPath)
        {
            try
            {
                for (string? current = fullFolderPath; current != null && Directory.Exists(current); current = Path.GetDirectoryName(current))
                {
                    // 空でない場合は IOException になり、他の出力が置かれたフォルダーを消さない
                    Directory.Delete(current);

                    if (string.Equals(current, createdRootPath, StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 後始末は最善努力とし、元の例外を優先する
            }
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


        /********************************************************************************/
        /*                                   ネスト型                                   */
        /********************************************************************************/
        /// <summary>
        /// 開いたまま再利用する PDFium の文書と、その使用数を管理する
        /// </summary>
        /// <remarks>
        /// キャッシュから外された (<see cref="Evict"/>) 後、使用中の参照がすべて解放された時点で文書を閉じる
        /// </remarks>
        private sealed class CachedDocument
        {
            /// <summary>使用数と破棄状態を保護するロックオブジェクト</summary>
            private readonly object _referenceLock = new object();

            /// <summary>文書を開いたファイルのパス</summary>
            private readonly string _filePath;

            /// <summary>文書を開いた時点のファイル最終更新日時 (UTC)</summary>
            private readonly DateTime _lastWrite;

            /// <summary>文書を開いた時点のファイルサイズ (バイト)</summary>
            private readonly long _fileSize;

            /// <summary>使用中の参照の数</summary>
            private int _referenceCount;

            /// <summary>キャッシュから外されたかどうか</summary>
            private bool _evicted;

            /// <summary>
            /// 開いた文書を、参照を 1 つ取得した状態で保持する
            /// </summary>
            /// <param name="document">開いた文書</param>
            /// <param name="filePath">文書を開いたファイルのパス</param>
            /// <param name="lastWrite">文書を開いた時点のファイル最終更新日時 (UTC)</param>
            /// <param name="fileSize">文書を開いた時点のファイルサイズ (バイト)</param>
            public CachedDocument(PdfiumDocument document, string filePath, DateTime lastWrite, long fileSize)
            {
                Document = document;
                _filePath = filePath;
                _lastWrite = lastWrite;
                _fileSize = fileSize;
                _referenceCount = 1;
            }

            /// <summary>PDFium の文書</summary>
            public PdfiumDocument Document { get; }

            /// <summary>
            /// 指定したファイルの現在の状態から開いた文書かどうかを返す
            /// </summary>
            /// <param name="filePath">ファイルのパス</param>
            /// <param name="lastWrite">ファイル最終更新日時 (UTC)</param>
            /// <param name="fileSize">ファイルサイズ (バイト)</param>
            /// <returns>一致する場合は <c>true</c></returns>
            public bool Matches(string filePath, DateTime lastWrite, long fileSize)
            {
                return string.Equals(_filePath, filePath, StringComparison.OrdinalIgnoreCase) && _lastWrite == lastWrite && _fileSize == fileSize;
            }

            /// <summary>
            /// 参照を 1 つ取得する
            /// </summary>
            /// <returns>取得できた場合は <c>true</c>、キャッシュから外されている場合は <c>false</c></returns>
            public bool TryAddReference()
            {
                lock (_referenceLock)
                {
                    if (_evicted)
                    {
                        return false;
                    }

                    _referenceCount++;
                    return true;
                }
            }

            /// <summary>
            /// 参照を 1 つ解放し、キャッシュから外されていて参照がなくなった場合は文書を閉じる
            /// </summary>
            public void Release()
            {
                bool shouldDispose;
                lock (_referenceLock)
                {
                    _referenceCount--;
                    shouldDispose = _evicted && _referenceCount == 0;
                }

                if (shouldDispose)
                {
                    Document.Dispose();
                }
            }

            /// <summary>
            /// キャッシュから外し、参照がなければ文書を閉じる
            /// </summary>
            public void Evict()
            {
                bool shouldDispose;
                lock (_referenceLock)
                {
                    if (_evicted)
                    {
                        return;
                    }

                    _evicted = true;
                    shouldDispose = _referenceCount == 0;
                }

                if (shouldDispose)
                {
                    Document.Dispose();
                }
            }
        }

        /// <summary>
        /// 保存先への反映対象 1 ファイル分の状態を保持し、置き換えと巻き戻しを行う
        /// </summary>
        /// <remarks>
        /// ステージング済みファイル・退避ファイルは保存先フォルダー内のステージングディレクトリに置くため、置き換えはすべて同一ボリューム内のリネームで完結する
        /// </remarks>
        private sealed class CommitEntry
        {
            /// <summary>反映先ファイルの絶対パス</summary>
            private readonly string _destinationPath;

            /// <summary>ステージング済みファイルの絶対パス</summary>
            private readonly string _stagedPath;

            /// <summary>既存の反映先ファイルの退避先の絶対パス</summary>
            private readonly string _backupPath;

            /// <summary>既存の反映先ファイルを退避済みの場合は <c>true</c></summary>
            private bool _backedUp;

            /// <summary>ステージング済みファイルを反映先へ配置済みの場合は <c>true</c></summary>
            private bool _installed;

            /// <summary>
            /// 反映先とステージング済みファイル・退避ファイルのパスを指定してインスタンスを生成する
            /// </summary>
            /// <param name="destinationPath">反映先ファイルの絶対パス</param>
            /// <param name="stagedPath">ステージング済みファイルの絶対パス</param>
            /// <param name="backupPath">既存の反映先ファイルの退避先の絶対パス</param>
            public CommitEntry(string destinationPath, string stagedPath, string backupPath)
            {
                _destinationPath = destinationPath;
                _stagedPath = stagedPath;
                _backupPath = backupPath;
            }

            /// <summary>
            /// 既存の反映先ファイルを退避したうえで、ステージング済みファイルを反映先へ配置する
            /// </summary>
            public void Install()
            {
                if (File.Exists(_destinationPath))
                {
                    File.Move(_destinationPath, _backupPath);
                    _backedUp = true;
                }

                File.Move(_stagedPath, _destinationPath);
                _installed = true;
            }

            /// <summary>
            /// 反映前の状態へ戻す (戻せなかった場合も例外は送出しない)
            /// </summary>
            public void Rollback()
            {
                TryRun(() =>
                {
                    if (_installed)
                    {
                        File.Delete(_destinationPath);
                    }
                });
                TryRun(() =>
                {
                    if (_backedUp)
                    {
                        File.Move(_backupPath, _destinationPath);
                    }
                });
            }

            /// <summary>
            /// ファイル操作を実行し、I/O 系の例外は無視する
            /// </summary>
            /// <param name="action">実行する操作</param>
            private static void TryRun(Action action)
            {
                try
                {
                    action();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // 後始末・巻き戻しは最善努力とし、元の例外を優先する
                }
            }
        }
    }
}
