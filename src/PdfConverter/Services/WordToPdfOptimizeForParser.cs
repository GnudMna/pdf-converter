using System;
using PdfConverter.Models;

namespace PdfConverter.Services
{
    /// <summary>
    /// <see cref="WordToPdfOptimizeFor"/> の文字列変換
    /// </summary>
    public static class WordToPdfOptimizeForParser
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 文字列を <see cref="WordToPdfOptimizeFor"/> に変換する
        /// </summary>
        /// <param name="value">設定値</param>
        /// <returns>出力最適化モード</returns>
        public static WordToPdfOptimizeFor Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return WordToPdfOptimizeFor.Print;
            }

            if (Enum.TryParse(value, ignoreCase: true, result: out WordToPdfOptimizeFor optimizeFor))
            {
                return optimizeFor;
            }

            return WordToPdfOptimizeFor.Print;
        }
    }
}
