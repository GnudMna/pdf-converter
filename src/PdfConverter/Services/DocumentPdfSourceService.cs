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
                int generation;

                lock (_cacheLock)
                {
                    generation = _cacheGeneration;

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
                }

                string pdfPath = await _wordToPdfService.ConvertToPdfAsync(normalizedPath, cancellationToken);

                lock (_cacheLock)
                {
                    if (generation == _cacheGeneration)
                    {
                        _pdfBySourcePath[normalizedPath] = new CachedPdf(pdfPath, sourceLastWriteUtc, sourceLength);
                        _temporaryPdfPaths.Add(pdfPath);
                        return pdfPath;
                    }
                }

                // 変換中に設定が変わったため、古い設定の PDF は使わずに破棄して現在の設定で変換し直す
                PdfConverterTempDirectory.TryDeleteFile(pdfPath);
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
    }
}
