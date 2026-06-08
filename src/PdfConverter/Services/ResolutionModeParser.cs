using System;
using PdfConverter.Models;

namespace PdfConverter.Services
{
    /// <summary>
    /// <see cref="ResolutionMode"/> の文字列変換
    /// </summary>
    public static class ResolutionModeParser
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 文字列を <see cref="ResolutionMode"/> に変換する
        /// </summary>
        /// <param name="value">設定文字列</param>
        /// <returns>解像度の指定方法</returns>
        public static ResolutionMode Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return ResolutionMode.Width;
            }

            if (Enum.TryParse(value, ignoreCase: true, result: out ResolutionMode mode)
                && mode != ResolutionMode.Default)
            {
                return mode;
            }

            return ResolutionMode.Width;
        }
    }
}
