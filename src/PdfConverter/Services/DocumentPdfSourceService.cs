using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PdfConverter.Services
{
    /// <summary>
    /// 入力ドキュメントからPDFレンダリング用のパスを提供するサービス
    /// </summary>
    public class DocumentPdfSourceService : IDocumentPdfSourceService, IDisposable
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>Word → PDF変換サービス</summary>
        private readonly IWordToPdfConversionService _wordToPdfService;

        /// <summary>Word → PDF変換エンジン設定</summary>
        private readonly IWordToPdfConversionSettings _settings;

        /// <summary>入力パスとPDFパスの対応表</summary>
        private readonly Dictionary<string, string> _pdfPathBySourcePath =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>一時PDFファイルのパス一覧</summary>
        private readonly HashSet<string> _temporaryPdfPaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>キャッシュ操作の排他制御用オブジェクト</summary>
        private readonly object _cacheLock = new object();

        /// <summary>設定変更時にキャッシュを破棄するハンドラー</summary>
        private readonly EventHandler _settingsChangedHandler;

        /// <summary>破棄済みかどうか</summary>
        private bool _disposed;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 指定したWord → PDF変換サービスを使用してPDFソースを提供する
        /// </summary>
        /// <param name="wordToPdfService">Word → PDF変換サービス</param>
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
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("入力ファイルのパスが指定されていません。", nameof(sourcePath));
            }

            if (!IsSupportedDocument(sourcePath))
            {
                throw new ArgumentException("PDF (.pdf) または Word (.doc / .docx) ファイルを指定してください。", nameof(sourcePath));
            }

            if (DocumentFileHelper.IsPdfFile(sourcePath))
            {
                return sourcePath;
            }

            lock (_cacheLock)
            {
                if (_pdfPathBySourcePath.TryGetValue(sourcePath, out string cachedPath) && File.Exists(cachedPath))
                {
                    return cachedPath;
                }
            }

            string pdfPath = await _wordToPdfService.ConvertToPdfAsync(sourcePath, cancellationToken);

            lock (_cacheLock)
            {
                _pdfPathBySourcePath[sourcePath] = pdfPath;
                _temporaryPdfPaths.Add(pdfPath);
            }

            return pdfPath;
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
                if (!_pdfPathBySourcePath.TryGetValue(sourcePath, out string pdfPath))
                {
                    return;
                }

                _pdfPathBySourcePath.Remove(sourcePath);

                if (_temporaryPdfPaths.Remove(pdfPath))
                {
                    TryDeleteFile(pdfPath);
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
                    TryDeleteFile(pdfPath);
                }

                _temporaryPdfPaths.Clear();
                _pdfPathBySourcePath.Clear();
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
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// 一時PDFファイルを削除する
        /// </summary>
        /// <param name="path">削除対象のファイルパス</param>
        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }
}
