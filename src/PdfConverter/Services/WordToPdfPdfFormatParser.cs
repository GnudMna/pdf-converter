using System;
using PdfConverter.Models;

namespace PdfConverter.Services
{
    /// <summary>
    /// <see cref="WordToPdfPdfFormat"/> の文字列変換
    /// </summary>
    public static class WordToPdfPdfFormatParser
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 文字列を <see cref="WordToPdfPdfFormat"/> に変換する
        /// </summary>
        /// <param name="value">設定値</param>
        /// <returns>PDF 形式</returns>
        public static WordToPdfPdfFormat Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return WordToPdfPdfFormat.Standard;
            }

            if (Enum.TryParse(value, ignoreCase: true, result: out WordToPdfPdfFormat format))
            {
                return format;
            }

            return WordToPdfPdfFormat.Standard;
        }
    }
}
