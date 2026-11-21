using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PdfConverter.Services
{
    /// <summary>
    /// 入力ドキュメントから PDF レンダリング用のパスを提供するサービス
    /// </summary>
    public class DocumentPdfSourceService : IDocumentPdfSourceService, IDisposable
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>Word → PDF 変換サービス</summary>
        private readonly IWordToPdfConversionService _wordToPdfService;

        /// <summary>Word → PDF 変換エンジン設定</summary>
        private readonly IWordToPdfConversionSettings _settings;

        /// <summary>入力パスと変換済み PDF の対応表</summary>
        private readonly Dictionary<string, CachedPdf> _pdfBySourcePath =
            new Dictionary<string, CachedPdf>(StringComparer.OrdinalIgnoreCase);

        /// <summary>入力パスと進行中の変換の対応表 (同じ入力への同時要求で変換を共有するため)</summary>
        private readonly Dictionary<string, PendingConversion> _pendingBySourcePath =
            new Dictionary<string, PendingConversion>(StringComparer.OrdinalIgnoreCase);

        /// <summary>一時 PDF ファイルのパス一覧</summary>
        private readonly HashSet<string> _temporaryPdfPaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>キャッシュ操作の排他制御用オブジェクト</summary>
        private readonly object _cacheLock = new object();

        /// <summary>設定変更時にキャッシュを破棄するハンドラー</summary>
        private readonly EventHandler _settingsChangedHandler;

        /// <summary>キャッシュを破棄するたびに進める世代番号 (変換中に設定が変わったことの検出用)</summary>
        private int _cacheGeneration;

        /// <summary>破棄済みかどうか</summary>
        private bool _disposed;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 指定した Word → PDF 変換サービスを使用して PDF ソースを提供する
        /// </summary>
        /// <param name="wordToPdfService">Word → PDF 変換サービス</param>
        /// <param name="settings">Word → PDF 変換設定</param>
        public DocumentPdfSourceService(
            IWordToPdfConversionService wordToPdfService,
            IWordToPdfConversionSettings settings)
        {
            _wordToPdfService = wordToPdfService;
            _settings = settings;
            _settingsChangedHandler = (_, __) => InvalidateAll();
            _settings.SettingsChanged += _settingsChangedHandler;
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        public bool IsSupportedDocument(string sourcePath)
        {
            return DocumentFileHelper.IsSupportedDocument(sourcePath);
        }

        /// <inheritdoc/>
        public async Task<string> GetPdfPathAsync(string sourcePath, CancellationToken cancellationToken = default)
        {
            string normalizedPath = DocumentFileHelper.ValidateAndNormalizeSupportedDocumentPath(
                sourcePath,
                nameof(sourcePath));

            if (DocumentFileHelper.IsPdfFile(normalizedPath))
            {
                return normalizedPath;
            }

            while (true)
            {
                // 変換中に編集された場合も次回再変換されるよう、変換前の状態を記録する
                var sourceInfo = new FileInfo(normalizedPath);
                DateTime sourceLastWriteUtc = sourceInfo.LastWriteTimeUtc;
                long sourceLength = sourceInfo.Length;
                PendingConversion pending;

                lock (_cacheLock)
                {
                    if (_pdfBySourcePath.TryGetValue(normalizedPath, out CachedPdf? cached))
                    {
                        if (cached.SourceLastWriteUtc == sourceLastWriteUtc
                            && cached.SourceLength == sourceLength
                            && File.Exists(cached.PdfPath))
                        {
                            return cached.PdfPath;
                        }

                        // 元ファイルが更新された、または一時 PDF が失われたため破棄して再変換する
                        RemoveCacheEntryUnsafe(normalizedPath, cached);
                    }

                    // 同じ入力を同じ状態・同じ設定で変換中なら、その結果を共有して二重に変換しない
                    if (!_pendingBySourcePath.TryGetValue(normalizedPath, out PendingConversion? existing)
                        || existing.Generation != _cacheGeneration
                        || existing.SourceLastWriteUtc != sourceLastWriteUtc
                        || existing.SourceLength != sourceLength)
                    {
                        existing = StartConversionUnsafe(normalizedPath, sourceLastWriteUtc, sourceLength);
                    }

                    existing.WaiterCount++;
                    pending = existing;
                }

                string? pdfPath = await WaitForConversionAsync(pending, cancellationToken);

                if (pdfPath != null)
                {
                    return pdfPath;
                }

                // 変換中に設定が変わった、または別の変換に置き換えられたため、現在の状態で変換し直す
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        /// <inheritdoc/>
        public void Invalidate(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return;
            }

            lock (_cacheLock)
            {
                // 進行中の変換は登録から外し、完了時に結果を破棄させる
                _pendingBySourcePath.Remove(sourcePath);

                if (_pdfBySourcePath.TryGetValue(sourcePath, out CachedPdf? cached))
                {
                    RemoveCacheEntryUnsafe(sourcePath, cached);
                }
            }
        }

        /// <summary>
        /// キャッシュ済みの一時 PDF をすべて破棄する
        /// </summary>
        public void InvalidateAll()
        {
            lock (_cacheLock)
            {
                foreach (string pdfPath in _temporaryPdfPaths)
                {
                    PdfConverterTempDirectory.TryDeleteFile(pdfPath);
                }

                _temporaryPdfPaths.Clear();
                _pdfBySourcePath.Clear();
                _pendingBySourcePath.Clear();
                _cacheGeneration++;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _settings.SettingsChanged -= _settingsChangedHandler;
            InvalidateAll();
            GC.SuppressFinalize(this);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// 変換を開始し、進行中の変換として登録する
        /// </summary>
        /// <param name="sourcePath">入力ファイルのパス</param>
        /// <param name="sourceLastWriteUtc">変換前の入力ファイルの最終更新日時 (UTC)</param>
        /// <param name="sourceLength">変換前の入力ファイルのサイズ (バイト)</param>
        /// <returns>登録した変換 (待機者数は 0)</returns>
        /// <remarks>呼び出し元で <see cref="_cacheLock"/> を取得していること</remarks>
        private PendingConversion StartConversionUnsafe(string sourcePath, DateTime sourceLastWriteUtc, long sourceLength)
        {
            var pending = new PendingConversion(sourcePath, _cacheGeneration, sourceLastWriteUtc, sourceLength);
            CancellationToken conversionToken = pending.CancellationToken;

            // ロックの中で変換サービスの同期部分を実行しないよう、スレッドプール上で呼び出す
            Task<string> conversion = Task.Run(
                () => _wordToPdfService.ConvertToPdfAsync(sourcePath, conversionToken),
                CancellationToken.None);

            _pendingBySourcePath[sourcePath] = pending;
            pending.Result = CompleteConversionAsync(sourcePath, pending, conversion);
            return pending;
        }

        /// <summary>
        /// 変換の完了を待ち、現行の変換であれば結果をキャッシュする
        /// </summary>
        /// <param name="sourcePath">入力ファイルのパス</param>
        /// <param name="pending">進行中の変換</param>
        /// <param name="conversion">変換サービスの実行</param>
        /// <returns>キャッシュした PDF のパス。変換中に設定が変わった、または別の変換に置き換えられた場合は null</returns>
        private async Task<string?> CompleteConversionAsync(string sourcePath, PendingConversion pending, Task<string> conversion)
        {
            string? pdfPath = null;
            bool cached = false;

            try
            {
                pdfPath = await conversion;
            }
            finally
            {
                lock (_cacheLock)
                {
                    bool isCurrent = _pendingBySourcePath.TryGetValue(sourcePath, out PendingConversion? registered)
                        && ReferenceEquals(registered, pending);

                    if (isCurrent)
                    {
                        _pendingBySourcePath.Remove(sourcePath);
                    }

                    if (pdfPath != null && isCurrent && pending.Generation == _cacheGeneration)
                    {
                        _pdfBySourcePath[sourcePath] = new CachedPdf(pdfPath, pending.SourceLastWriteUtc, pending.SourceLength);
                        _temporaryPdfPaths.Add(pdfPath);
                        cached = true;
                    }

                    pending.Dispose();
                }
            }

            if (cached)
            {
                return pdfPath;
            }

            // 使われない PDF は残さず破棄する
            if (pdfPath != null)
            {
                PdfConverterTempDirectory.TryDeleteFile(pdfPath);
            }

            return null;
        }

        /// <summary>
        /// 進行中の変換の完了を待つ。待機者がすべてキャンセルした場合は変換自体も中断する
        /// </summary>
        /// <param name="pending">進行中の変換</param>
        /// <param name="cancellationToken">待機をキャンセルするためのトークン</param>
        /// <returns>PDF のパス。結果を使えない場合は null</returns>
        private async Task<string?> WaitForConversionAsync(PendingConversion pending, CancellationToken cancellationToken)
        {
            try
            {
                return await pending.Result.WaitAsync(cancellationToken);
            }
            finally
            {
                lock (_cacheLock)
                {
                    pending.WaiterCount--;

                    if (pending.WaiterCount == 0)
                    {
                        // 誰も結果を待たない変換は、後から来た要求が合流しないよう登録から外して中断する
                        if (_pendingBySourcePath.TryGetValue(pending.SourcePathKey, out PendingConversion? registered)
                            && ReferenceEquals(registered, pending))
                        {
                            _pendingBySourcePath.Remove(pending.SourcePathKey);
                        }

                        pending.CancelIfRunning();
                    }
                }
            }
        }

        /// <summary>
        /// キャッシュエントリを削除し、対応する一時 PDF を破棄する
        /// </summary>
        /// <param name="sourcePath">入力ファイルのパス</param>
        /// <param name="cached">削除するキャッシュエントリ</param>
        /// <remarks>呼び出し元で <see cref="_cacheLock"/> を取得していること</remarks>
        private void RemoveCacheEntryUnsafe(string sourcePath, CachedPdf cached)
        {
            _pdfBySourcePath.Remove(sourcePath);

            if (_temporaryPdfPaths.Remove(cached.PdfPath))
            {
                PdfConverterTempDirectory.TryDeleteFile(cached.PdfPath);
            }
        }


        /********************************************************************************/
        /*                                   ネスト型                                   */
        /********************************************************************************/
        /// <summary>変換済み PDF と、変換時点の入力ファイルの状態</summary>
        private sealed class CachedPdf
        {
            /// <summary>
            /// キャッシュエントリを初期化する
            /// </summary>
            /// <param name="pdfPath">変換済み PDF のパス</param>
            /// <param name="sourceLastWriteUtc">変換時点の入力ファイルの最終更新日時 (UTC)</param>
            /// <param name="sourceLength">変換時点の入力ファイルのサイズ (バイト)</param>
            public CachedPdf(string pdfPath, DateTime sourceLastWriteUtc, long sourceLength)
            {
                PdfPath = pdfPath;
                SourceLastWriteUtc = sourceLastWriteUtc;
                SourceLength = sourceLength;
            }

            /// <summary>変換済み PDF のパス</summary>
            public string PdfPath { get; }

            /// <summary>変換時点の入力ファイルの最終更新日時 (UTC)</summary>
            public DateTime SourceLastWriteUtc { get; }

            /// <summary>変換時点の入力ファイルのサイズ (バイト)</summary>
            public long SourceLength { get; }
        }

        /// <summary>進行中の変換。同じ入力への要求が結果を共有する</summary>
        private sealed class PendingConversion : IDisposable
        {
            /// <summary>変換を中断するためのソース (待機者がすべてキャンセルしたときに使う)</summary>
            private CancellationTokenSource? _cancellationSource = new CancellationTokenSource();

            /// <summary>変換サービスへ渡すキャンセルトークン</summary>
            private readonly CancellationToken _cancellationToken;

            /// <summary>
            /// 進行中の変換を初期化する
            /// </summary>
            /// <param name="sourcePathKey">入力ファイルのパス</param>
            /// <param name="generation">変換開始時のキャッシュ世代番号</param>
            /// <param name="sourceLastWriteUtc">変換前の入力ファイルの最終更新日時 (UTC)</param>
            /// <param name="sourceLength">変換前の入力ファイルのサイズ (バイト)</param>
            public PendingConversion(string sourcePathKey, int generation, DateTime sourceLastWriteUtc, long sourceLength)
            {
                SourcePathKey = sourcePathKey;
                Generation = generation;
                SourceLastWriteUtc = sourceLastWriteUtc;
                SourceLength = sourceLength;
                _cancellationToken = _cancellationSource.Token;
            }

            /// <summary>入力ファイルのパス</summary>
            public string SourcePathKey { get; }

            /// <summary>変換開始時のキャッシュ世代番号</summary>
            public int Generation { get; }

            /// <summary>変換前の入力ファイルの最終更新日時 (UTC)</summary>
            public DateTime SourceLastWriteUtc { get; }

            /// <summary>変換前の入力ファイルのサイズ (バイト)</summary>
            public long SourceLength { get; }

            /// <summary>変換サービスへ渡すキャンセルトークン</summary>
            public CancellationToken CancellationToken => _cancellationToken;

            /// <summary>結果を待っている要求の数 (キャッシュの排他制御の中で更新する)</summary>
            public int WaiterCount { get; set; }

            /// <summary>変換の結果 (使えない場合は null)</summary>
            public Task<string?> Result { get; set; } = Task.FromResult<string?>(null);

            /// <summary>
            /// 変換が実行中であれば中断を要求する
            /// </summary>
            public void CancelIfRunning()
            {
                _cancellationSource?.Cancel();
            }

            /// <summary>
            /// キャンセルトークンのソースを解放する (変換の終了時に呼ぶ)
            /// </summary>
            public void Dispose()
            {
                _cancellationSource?.Dispose();
                _cancellationSource = null;
            }
        }
    }
}
