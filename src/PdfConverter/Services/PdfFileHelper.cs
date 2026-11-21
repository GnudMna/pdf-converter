using System;
using System.Linq;
using System.Windows;

namespace PdfConverter.Services
{
    /// <summary>
    /// PDFファイルパスの判定に関する共通処理
    /// </summary>
    public static class PdfFileHelper
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
            return !string.IsNullOrEmpty(path)
                && string.Equals(System.IO.Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// ドラッグ中のデータにPDFファイルが含まれるかどうかを判定する
        /// </summary>
        /// <param name="data">ドラッグ中のデータ</param>
        /// <returns>true: PDFファイルが含まれている / false: PDFファイルが含まれていない</returns>
        public static bool ContainsPdfFile(IDataObject data)
        {
            return TryGetFirstPdfPath(data, out _);
        }

        /// <summary>
        /// ドラッグ中のデータから先頭のPDFファイルパスを取得する
        /// </summary>
        /// <param name="data">ドラッグ中のデータ</param>
        /// <param name="filePath">取得したPDFファイルのパス</param>
        /// <returns>true: 取得できた / false: 取得できなかった</returns>
        public static bool TryGetFirstPdfPath(IDataObject data, out string filePath)
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

            filePath = files.FirstOrDefault(IsPdfFile);
            return !string.IsNullOrEmpty(filePath);
        }
    }
}
