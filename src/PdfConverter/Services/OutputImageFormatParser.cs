using System;
using PdfConverter.Models;

namespace PdfConverter.Services
{
    /// <summary>
    /// <see cref="OutputImageFormat"/> の文字列変換
    /// </summary>
    public static class OutputImageFormatParser
    {
        /// <summary>
        /// 文字列を <see cref="OutputImageFormat"/> に変換する
        /// </summary>
        /// <param name="value">設定文字列</param>
        /// <returns>出力画像形式</returns>
        public static OutputImageFormat Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return OutputImageFormat.Png;
            }

            if (Enum.TryParse(value, ignoreCase: true, result: out OutputImageFormat format))
            {
                return format;
            }

            return OutputImageFormat.Png;
        }
    }
}
