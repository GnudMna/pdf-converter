using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace PdfConverter.Services
{
    /// <summary>
    /// 入力ドキュメント (PDF / Word) のパス判定に関する共通処理
    /// </summary>
    public static class DocumentFileHelper
    {
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

            if (!File.Exists(wordFilePath))
            {
                throw new FileNotFoundException("Word ファイルが見つかりません。", wordFilePath);
            }

            if (!IsWordFile(wordFilePath))
            {
                throw new ArgumentException("Word ファイル (.doc / .docx) を指定してください。", nameof(wordFilePath));
            }
        }

        /// <summary>
        /// ドラッグ中のデータにサポート対象のドキュメントが含まれるかどうかを判定する
        /// </summary>
        /// <param name="data">ドラッグ中のデータ</param>
        /// <returns>true: サポート対象のドキュメントが含まれている / false: 含まれていない</returns>
        public static bool ContainsSupportedDocument(IDataObject data)
        {
            return TryGetFirstSupportedPath(data, out _);
        }

        /// <summary>
        /// ドラッグ中のデータから先頭のサポート対象ドキュメントのパスを取得する
        /// </summary>
        /// <param name="data">ドラッグ中のデータ</param>
        /// <param name="filePath">取得したファイルのパス</param>
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

            filePath = files.FirstOrDefault(IsSupportedDocument);
            return !string.IsNullOrEmpty(filePath);
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

            string extension = System.IO.Path.GetExtension(path);
            return extensions.Any(ext => string.Equals(extension, ext, StringComparison.OrdinalIgnoreCase));
        }
    }
}
