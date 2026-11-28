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
        /// <exception cref="FormatException">番号や範囲の書式が不正な場合、ページ番号が PDF のページ数を超える場合、または解析結果が空の場合</exception>
        public static IReadOnlyList<int> Parse(string pageRange, int pageCount)
        {
            var pages = new List<int>();
            foreach (var (start, end) in ParseRanges(pageRange, pageCount))
            {
                for (int i = start; i <= end; i++)
                {
                    pages.Add(i - 1);
                }
            }

            return pages.Distinct().OrderBy(x => x).ToList();
        }

        /// <summary>
        /// 総ページ数に依存しない誤り (書式・空・逆順など) だけを、ページを展開せずに検証する
        /// </summary>
        /// <param name="pageRange">カンマ区切りのページ番号・範囲文字列</param>
        /// <exception cref="FormatException">書式が不正な場合、または解析結果が空の場合</exception>
        public static void ValidateFormat(string pageRange)
        {
            _ = ParseRanges(pageRange, int.MaxValue);
        }


        /********************************************************************************/
        /*                            プライベートメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// ページ範囲文字列を、両端を含む 1 始まりの (開始, 終了) 一覧へ解析する
        /// </summary>
        /// <param name="pageRange">カンマ区切りのページ番号・範囲文字列</param>
        /// <param name="pageCount">PDF の総ページ数 (範囲検証と終わりを省いた範囲の補完に使用)</param>
        /// <returns>解析した範囲の一覧</returns>
        private static List<(int Start, int End)> ParseRanges(string pageRange, int pageCount)
        {
            var ranges = new List<(int Start, int End)>();
            foreach (var token in NormalizeFullWidth(pageRange).Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var item = token.Trim();
                if (string.IsNullOrEmpty(item))
                {
                    continue;
                }

                if (item.Contains('-'))
                {
                    var bounds = item.Split('-');
                    if (bounds.Length != 2
                        || !TryParseNumber(bounds[0].Trim(), out int start))
                    {
                        throw new FormatException("範囲は '2-5' または '5-' のように指定してください。");
                    }

                    // 終わりを省いた範囲 ("5-") は最終ページまでとして扱う
                    var endText = bounds[1].Trim();
                    int end = pageCount;
                    if (endText.Length > 0 && !TryParseNumber(endText, out end))
                    {
                        throw new FormatException("範囲は '2-5' または '5-' のように指定してください。");
                    }

                    if (start < 1 || end < 1 || start > end || end > pageCount)
                    {
                        throw new FormatException("ページ範囲が無効です。PDF のページ数内で指定してください。");
                    }

                    ranges.Add((start, end));
                }
                else
                {
                    if (!TryParseNumber(item, out int page))
                    {
                        throw new FormatException("ページ番号は数字で指定してください。");
                    }

                    if (page < 1 || page > pageCount)
                    {
                        throw new FormatException("ページ番号が PDF のページ数の範囲外です。");
                    }

                    ranges.Add((page, page));
                }
            }

            return ranges.Count > 0
                ? ranges
                : throw new FormatException("ページ範囲が空です。");
        }

        /// <summary>
        /// 全角の数字・区切り文字を半角へ置き換える
        /// </summary>
        /// <remarks>
        /// 日本語入力のまま打つと、ハイフンは「－」(全角モード) や「ー」(ひらがなモード)、カンマは「，」や「、」になるため、これらも受け付ける
        /// </remarks>
        /// <param name="text">置き換え対象の文字列</param>
        /// <returns>全角の数字・区切り文字を半角にした文字列</returns>
        private static string NormalizeFullWidth(string text)
        {
            return string.Create(text.Length, text, static (span, source) =>
            {
                for (int i = 0; i < span.Length; i++)
                {
                    var c = source[i];
                    span[i] = c switch
                    {
                        >= '０' and <= '９' => (char)('0' + (c - '０')),
                        '－' or 'ー' or '−' => '-',
                        '，' or '、' => ',',
                        _ => c,
                    };
                }
            });
        }

        /// <summary>
        /// 半角数字のみで構成された文字列を整数へ変換する (符号や空文字は不可)
        /// </summary>
        /// <param name="text">変換対象の文字列</param>
        /// <param name="value">変換結果</param>
        /// <returns>変換できた場合は true</returns>
        private static bool TryParseNumber(string text, out int value)
        {
            value = 0;
            return text.Length > 0
                && text.All(char.IsAsciiDigit)
                && int.TryParse(text, out value);
        }
    }
}
