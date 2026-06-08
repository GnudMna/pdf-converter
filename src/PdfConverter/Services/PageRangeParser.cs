using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfConverter.Services
{
    /// <summary>
    /// "1,3-5" 形式のページ範囲文字列を解析するユーティリティ
    /// </summary>
    public static class PageRangeParser
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// ページ範囲文字列を解析して 0 始まりのページインデックス一覧を返す
        /// </summary>
        /// <param name="pageRange">カンマ区切りのページ番号・範囲文字列</param>
        /// <param name="pageCount">PDF の総ページ数 (範囲検証に使用)</param>
        /// <returns>重複排除・昇順ソート済みの 0 始まりページインデックス一覧</returns>
        /// <exception cref="FormatException">番号や範囲の書式が不正な場合</exception>
        /// <exception cref="ArgumentOutOfRangeException">ページ番号が PDF のページ数を超える場合</exception>
        /// <exception cref="ArgumentException">解析結果が空の場合</exception>
        public static IReadOnlyList<int> Parse(string pageRange, int pageCount)
        {
            var pages = new List<int>();
            foreach (var token in pageRange.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var item = token.Trim();
                if (string.IsNullOrEmpty(item))
                {
                    continue;
                }

                if (item.Contains("-"))
                {
                    var bounds = item.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                    if (bounds.Length != 2
                        || !int.TryParse(bounds[0], out int start)
                        || !int.TryParse(bounds[1], out int end))
                    {
                        throw new FormatException("範囲は '2-5' のように指定してください。");
                    }

                    if (start < 1 || end < 1 || start > end || end > pageCount)
                    {
                        throw new ArgumentOutOfRangeException(nameof(pageRange), "ページ範囲が無効です。PDF のページ数内で指定してください。");
                    }

                    for (int i = start; i <= end; i++)
                    {
                        pages.Add(i - 1);
                    }
                }
                else
                {
                    if (!int.TryParse(item, out int page))
                    {
                        throw new FormatException("ページ番号は数字で指定してください。");
                    }

                    if (page < 1 || page > pageCount)
                    {
                        throw new ArgumentOutOfRangeException(nameof(pageRange), "ページ番号が PDF のページ数の範囲外です。");
                    }

                    pages.Add(page - 1);
                }
            }

            return pages.Count > 0
                ? pages.Distinct().OrderBy(x => x).ToList()
                : throw new ArgumentException("ページ範囲が空です。", nameof(pageRange));
        }
    }
}
