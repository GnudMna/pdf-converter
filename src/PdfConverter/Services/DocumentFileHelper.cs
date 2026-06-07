using System;
using System.Linq;
using System.Windows;

namespace PdfConverter.Services
{
    /// <summary>
    /// 入力ドキュメント（PDF / Word）のパス判定に関する共通処理
    /// </summary>
    public static class DocumentFileHelper
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 指定したパスがPDFファイルかどうかを判定する
        /// </summary>
        /// <param name="path">ファイルパス</param>
        /// <returns>true: PDFファイル / false: PDFファイルではない</returns>
        public static bool IsPdfFile(string path)
        {
            return HasExtension(path, ".pdf");
        }

        /// <summary>
        /// 指定したパスがWordファイルかどうかを判定する
        /// </summary>
        /// <param name="path">ファイルパス</param>
        /// <returns>true: Wordファイル / false: Wordファイルではない</returns>
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
        /// <param name="extensions">拡張子一覧（先頭にドットを含む）</param>
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
