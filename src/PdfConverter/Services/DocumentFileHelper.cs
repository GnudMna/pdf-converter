using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace PdfConverter.Services
{
    /// <summary>
    /// サポート対象ドキュメントのパス検証結果
    /// </summary>
    public enum SupportedDocumentPathError
    {
        /// <summary>検証成功</summary>
        None,

        /// <summary>パスが空</summary>
        Empty,

        /// <summary>サポート対象外の拡張子</summary>
        UnsupportedExtension,

        /// <summary>パスを絶対パスに正規化できない</summary>
        InvalidPath,

        /// <summary>ファイルが存在しない</summary>
        FileNotFound,
    }

    /// <summary>
    /// 入力ドキュメント (PDF / Word) のパス判定に関する共通処理
    /// </summary>
    public static class DocumentFileHelper
    {
        /********************************************************************************/
        /*                                 ローカル定数                                 */
        /********************************************************************************/
        /// <summary>サポート対象外拡張子のエラーメッセージ</summary>
        internal const string UnsupportedExtensionMessage =
            "PDF (.pdf) または Word (.doc / .docx) ファイルを指定してください。";

        /// <summary>ファイル未存在のエラーメッセージ</summary>
        internal const string FileNotFoundMessage = "ファイルが見つかりません。パスを確認してください。";

        /// <summary>パス不正のエラーメッセージ</summary>
        internal const string InvalidPathMessage = "ファイルパスが不正です。";


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 指定したパスが PDF ファイルかどうかを判定する
        /// </summary>
        /// <param name="path">ファイルパス</param>
        /// <returns>true: PDF ファイル / false: PDF ファイルではない</returns>
        public static bool IsPdfFile(string path)
        {
            return HasExtension(path, ".pdf");
        }

        /// <summary>
        /// 指定したパスが Word ファイルかどうかを判定する
        /// </summary>
        /// <param name="path">ファイルパス</param>
        /// <returns>true: Word ファイル / false: Word ファイルではない</returns>
        public static bool IsWordFile(string path)
        {
            return HasExtension(path, ".doc", ".docx");
        }

        /// <summary>
        /// 指定したパスがサポート対象のドキュメントかどうかを判定する
        /// </summary>
        /// <param name="path">ファイルパス</param>
        /// <returns>true: サポート対象 / false: サポート対象ではない</returns>
        public static bool IsSupportedDocument(string path)
        {
            return IsPdfFile(path) || IsWordFile(path);
        }

        /// <summary>
        /// サポート対象ドキュメントのパスを検証し、絶対パスに正規化する
        /// </summary>
        /// <param name="path">入力パス</param>
        /// <param name="normalizedPath">正規化後の絶対パス</param>
        /// <returns>検証結果</returns>
        public static SupportedDocumentPathError ResolveSupportedDocumentPath(string path, out string normalizedPath)
        {
            normalizedPath = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                return SupportedDocumentPathError.Empty;
            }

            string trimmedPath = path.Trim().Trim('"');
            if (!IsSupportedDocument(trimmedPath))
            {
                return SupportedDocumentPathError.UnsupportedExtension;
            }

            if (!TryGetFullPath(trimmedPath, out normalizedPath))
            {
                return SupportedDocumentPathError.InvalidPath;
            }

            if (!File.Exists(normalizedPath))
            {
                return SupportedDocumentPathError.FileNotFound;
            }

            return SupportedDocumentPathError.None;
        }

        /// <summary>
        /// サポート対象ドキュメントのパスを検証し、絶対パスに正規化する
        /// </summary>
        /// <param name="path">入力パス</param>
        /// <param name="normalizedPath">正規化後の絶対パス</param>
        /// <returns>true: 検証成功 / false: 検証失敗</returns>
        public static bool TryResolveSupportedDocumentPath(string path, out string normalizedPath)
        {
            normalizedPath = null;
            if (ResolveSupportedDocumentPath(path, out string resolvedPath) != SupportedDocumentPathError.None)
            {
                return false;
            }

            normalizedPath = resolvedPath;
            return true;
        }

        /// <summary>
        /// パス検証結果に対応するユーザー向けメッセージを返す
        /// </summary>
        /// <param name="error">検証結果</param>
        /// <returns>エラーメッセージ</returns>
        /// <exception cref="ArgumentOutOfRangeException">未対応の検証結果が指定された場合</exception>
        public static string GetSupportedDocumentPathErrorMessage(SupportedDocumentPathError error)
        {
            switch (error)
            {
                case SupportedDocumentPathError.UnsupportedExtension:
                    return UnsupportedExtensionMessage;
                case SupportedDocumentPathError.InvalidPath:
                    return InvalidPathMessage;
                case SupportedDocumentPathError.FileNotFound:
                    return FileNotFoundMessage;
                case SupportedDocumentPathError.Empty:
                case SupportedDocumentPathError.None:
                    return string.Empty;
                default:
                    throw new ArgumentOutOfRangeException(nameof(error), error, null);
            }
        }

        /// <summary>
        /// サポート対象ドキュメントのパスを検証し、絶対パスを返す
        /// </summary>
        /// <param name="documentPath">入力パス</param>
        /// <param name="paramName">例外に含めるパラメーター名</param>
        /// <returns>正規化後の絶対パス</returns>
        /// <exception cref="ArgumentException">パスが空、拡張子不正、またはパス不正の場合</exception>
        /// <exception cref="FileNotFoundException">ファイルが存在しない場合</exception>
        public static string ValidateAndNormalizeSupportedDocumentPath(string documentPath, string paramName = "documentPath")
        {
            SupportedDocumentPathError error = ResolveSupportedDocumentPath(documentPath, out string normalizedPath);
            switch (error)
            {
                case SupportedDocumentPathError.None:
                    return normalizedPath;
                case SupportedDocumentPathError.Empty:
                    throw new ArgumentException("入力ファイルのパスが指定されていません。", paramName);
                case SupportedDocumentPathError.UnsupportedExtension:
                    throw new ArgumentException(UnsupportedExtensionMessage, paramName);
                case SupportedDocumentPathError.InvalidPath:
                    throw new ArgumentException(InvalidPathMessage, paramName);
                case SupportedDocumentPathError.FileNotFound:
                    throw new FileNotFoundException(FileNotFoundMessage, normalizedPath);
                default:
                    throw new ArgumentOutOfRangeException(nameof(error), error, null);
            }
        }

        /// <summary>
        /// Word → PDF 変換用のファイルパスを検証する
        /// </summary>
        /// <param name="wordFilePath">Word ファイルの絶対パス</param>
        /// <exception cref="ArgumentException">パスが空、または Word ファイルではない場合</exception>
        /// <exception cref="FileNotFoundException">ファイルが存在しない場合</exception>
        public static void ValidateWordFilePath(string wordFilePath)
        {
            if (string.IsNullOrWhiteSpace(wordFilePath))
            {
                throw new ArgumentException("Word ファイルのパスが指定されていません。", nameof(wordFilePath));
            }

            string trimmedPath = wordFilePath.Trim().Trim('"');
            if (!IsWordFile(trimmedPath))
            {
                throw new ArgumentException("Word ファイル (.doc / .docx) を指定してください。", nameof(wordFilePath));
            }

            if (!TryGetFullPath(trimmedPath, out string normalizedPath))
            {
                throw new ArgumentException("Word ファイルのパスが不正です。", nameof(wordFilePath));
            }

            if (!File.Exists(normalizedPath))
            {
                throw new FileNotFoundException("Word ファイルが見つかりません。", normalizedPath);
            }
        }

        /// <summary>
        /// コマンドライン引数から先頭の有効なサポート対象ドキュメントのパスを取得する
        /// </summary>
        /// <param name="args">起動時に渡されたコマンドライン引数</param>
        /// <param name="filePath">取得したファイルの正規化済み絶対パス</param>
        /// <returns>true: 取得できた / false: 取得できなかった</returns>
        public static bool TryGetFirstSupportedPathFromArgs(string[] args, out string filePath)
        {
            filePath = null;

            if (args == null || args.Length == 0)
            {
                return false;
            }

            foreach (string arg in args)
            {
                if (string.IsNullOrWhiteSpace(arg))
                {
                    continue;
                }

                if (TryResolveSupportedDocumentPath(arg, out filePath))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// ドラッグ中のデータに有効なサポート対象ドキュメントが含まれるかどうかを判定する
        /// </summary>
        /// <param name="data">ドラッグ中のデータ</param>
        /// <returns>true: サポート対象のドキュメントが含まれている / false: 含まれていない</returns>
        public static bool ContainsSupportedDocument(IDataObject data)
        {
            return TryGetFirstSupportedPath(data, out _);
        }

        /// <summary>
        /// ドラッグ中のデータから先頭の有効なサポート対象ドキュメントのパスを取得する
        /// </summary>
        /// <param name="data">ドラッグ中のデータ</param>
        /// <param name="filePath">取得したファイルの正規化済み絶対パス</param>
        /// <returns>true: 取得できた / false: 取得できなかった</returns>
        public static bool TryGetFirstSupportedPath(IDataObject data, out string filePath)
        {
            filePath = null;

            if (data == null || !data.GetDataPresent(DataFormats.FileDrop))
            {
                return false;
            }

            if (!(data.GetData(DataFormats.FileDrop) is string[] files))
            {
                return false;
            }

            foreach (string candidate in files)
            {
                if (TryResolveSupportedDocumentPath(candidate, out filePath))
                {
                    return true;
                }
            }

            return false;
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// 指定した拡張子のいずれかに一致するかどうかを判定する
        /// </summary>
        /// <param name="path">ファイルパス</param>
        /// <param name="extensions">拡張子一覧 (先頭にドットを含む)</param>
        /// <returns>true: 一致 / false: 一致しない</returns>
        private static bool HasExtension(string path, params string[] extensions)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            string extension = Path.GetExtension(path);
            return extensions.Any(ext => string.Equals(extension, ext, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// パスを絶対パスに正規化する
        /// </summary>
        /// <param name="path">入力パス</param>
        /// <param name="fullPath">正規化後の絶対パス</param>
        /// <returns>true: 正規化できた / false: 正規化できなかった</returns>
        private static bool TryGetFullPath(string path, out string fullPath)
        {
            fullPath = null;

            try
            {
                fullPath = Path.GetFullPath(path);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }
    }
}
