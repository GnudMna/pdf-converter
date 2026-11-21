using System;
using System.Globalization;
using PdfConverter.Models;

namespace PdfConverter.Services
{
    /// <summary>
    /// ページ画像の出力ファイル名を生成するユーティリティ
    /// </summary>
    public static class PageImageFileNameHelper
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// ページ画像の出力ファイル名を返す
        /// </summary>
        /// <remarks>
        /// 名前順で並べてもページ順になるよう、ページ番号を総ページ数の桁数までゼロ埋めする<br/>
        /// 例: 総ページ数 12 の 3 ページ目は <c>page_03.png</c>
        /// </remarks>
        /// <param name="pageIndex">ページインデックス (0 始まり)</param>
        /// <param name="pageCount">PDF の総ページ数</param>
        /// <param name="format">出力画像形式</param>
        /// <returns>出力ファイル名 (拡張子を含む)</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> が範囲外の場合</exception>
        public static string GetFileName(int pageIndex, int pageCount, OutputImageFormat format)
        {
            if (pageIndex < 0 || pageIndex >= pageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(pageIndex), pageIndex, "ページインデックスが範囲外です。");
            }

            int digitCount = pageCount.ToString(CultureInfo.InvariantCulture).Length;
            string pageNumber = (pageIndex + 1).ToString(CultureInfo.InvariantCulture).PadLeft(digitCount, '0');
            return $"page_{pageNumber}{ImageBitmapHelper.GetFileExtension(format)}";
        }
    }
}
