using System;

namespace PdfConverter.Services
{
    /// <summary>
    /// Word → PDF 変換のタイムアウト設定
    /// </summary>
    internal static class WordToPdfConversionTimeouts
    {
        /// <summary>変換処理全体の上限時間</summary>
        public static readonly TimeSpan Conversion = TimeSpan.FromMinutes(5);
    }
}
