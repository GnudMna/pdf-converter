using System;
using PdfConverter.Models;

namespace PdfConverter.Services
{
    /// <summary>
    /// <see cref="WordToPdfBackend"/>の文字列変換
    /// </summary>
    public static class WordToPdfBackendParser
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 文字列を<see cref="WordToPdfBackend"/>に変換する
        /// </summary>
        /// <param name="value">設定文字列</param>
        /// <returns>変換エンジン</returns>
        public static WordToPdfBackend Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return WordToPdfBackend.MicrosoftWord;
            }

            if (Enum.TryParse(value, ignoreCase: true, result: out WordToPdfBackend backend))
            {
                return backend;
            }

            return WordToPdfBackend.MicrosoftWord;
        }
    }
}
