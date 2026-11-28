using System;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="PageRangeParser"/> のページ範囲文字列解析を検証する
    /// </summary>
    public class PageRangeParserTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 単一ページ番号 "3" が 0 始まりインデックス 2 に変換されることを検証する
        /// </summary>
        [Fact]
        public void Parse_SinglePage_ReturnsZeroBasedIndex()
        {
            var result = PageRangeParser.Parse("3", 10);

            Assert.Equal(new[] { 2 }, result);
        }

        /// <summary>
        /// 範囲指定 "2-4" が両端を含む 0 始まりインデックス [1,2,3] に変換されることを検証する
        /// </summary>
        [Fact]
        public void Parse_Range_ReturnsInclusiveZeroBasedIndexes()
        {
            var result = PageRangeParser.Parse("2-4", 10);

            Assert.Equal(new[] { 1, 2, 3 }, result);
        }

        /// <summary>
        /// 単一ページと範囲の混在入力で重複が排除され、昇順にソートされることを検証する
        /// </summary>
        [Fact]
        public void Parse_MixedInput_ReturnsSortedDistinctIndexes()
        {
            var result = PageRangeParser.Parse("1,3-5,3", 10);

            Assert.Equal(new[] { 0, 2, 3, 4 }, result);
        }

        /// <summary>
        /// カンマ区切りトークン前後の空白がトリムされて正しく解析されることを検証する
        /// </summary>
        [Fact]
        public void Parse_TrimsWhitespaceAroundTokens()
        {
            var result = PageRangeParser.Parse(" 1 , 2 ", 5);

            Assert.Equal(new[] { 0, 1 }, result);
        }

        /// <summary>
        /// ページ番号が 1 未満または総ページ数を超える場合に引数名を含まない FormatException がスローされることを検証する
        /// </summary>
        [Theory]
        [InlineData("0")]
        [InlineData("11")]
        public void Parse_PageOutOfRange_ThrowsFormatException(string pageRange)
        {
            Action act = () => PageRangeParser.Parse(pageRange, 10);

            var ex = Assert.Throws<FormatException>(act);
            Assert.DoesNotContain("Parameter", ex.Message);
        }

        /// <summary>
        /// 逆順・範囲外など不正な範囲指定で引数名を含まない FormatException がスローされることを検証する
        /// </summary>
        [Theory]
        [InlineData("0-2")]
        [InlineData("5-2")]
        [InlineData("11-12")]
        public void Parse_InvalidRange_ThrowsFormatException(string pageRange)
        {
            Action act = () => PageRangeParser.Parse(pageRange, 10);

            var ex = Assert.Throws<FormatException>(act);
            Assert.DoesNotContain("Parameter", ex.Message);
        }

        /// <summary>
        /// 全角数字の指定が半角数字と同じ結果になることを検証する
        /// </summary>
        [Fact]
        public void Parse_FullWidthDigits_ReturnsSameAsHalfWidth()
        {
            var result = PageRangeParser.Parse("１-３,５", 10);

            Assert.Equal(new[] { 0, 1, 2, 4 }, result);
        }

        /// <summary>
        /// 全角・日本語入力のハイフンとカンマが半角と同じ結果になることを検証する
        /// </summary>
        [Theory]
        [InlineData("１－３，５")]
        [InlineData("１ー３、５")]
        [InlineData("1−3,5")]
        public void Parse_FullWidthSeparators_ReturnsSameAsHalfWidth(string pageRange)
        {
            var result = PageRangeParser.Parse(pageRange, 10);

            Assert.Equal(new[] { 0, 1, 2, 4 }, result);
        }

        /// <summary>
        /// 全角のハイフンが重なる範囲指定で FormatException がスローされることを検証する
        /// </summary>
        [Fact]
        public void Parse_DoubledFullWidthHyphen_ThrowsFormatException()
        {
            Action act = () => PageRangeParser.Parse("１－－３", 10);

            Assert.Throws<FormatException>(act);
        }

        /// <summary>
        /// 終わりを省いた範囲 "8-" が最終ページまでに展開されることを検証する
        /// </summary>
        [Fact]
        public void Parse_OpenEndedRange_ExpandsToLastPage()
        {
            var result = PageRangeParser.Parse("8-", 10);

            Assert.Equal(new[] { 7, 8, 9 }, result);
        }

        /// <summary>
        /// 区切りが重複・不足した範囲指定で FormatException がスローされることを検証する
        /// </summary>
        [Theory]
        [InlineData("1--3")]
        [InlineData("1-3-")]
        [InlineData("-3")]
        [InlineData("-")]
        [InlineData("11-")]
        public void Parse_MalformedRange_ThrowsFormatException(string pageRange)
        {
            Action act = () => PageRangeParser.Parse(pageRange, 10);

            Assert.Throws<FormatException>(act);
        }

        /// <summary>
        /// 数字以外のページ番号指定で FormatException がスローされることを検証する
        /// </summary>
        [Fact]
        public void Parse_NonNumericPage_ThrowsFormatException()
        {
            Action act = () => PageRangeParser.Parse("a", 10);

            var ex = Assert.Throws<FormatException>(act);
            Assert.Contains("数字", ex.Message);
        }

        /// <summary>
        /// 有効なページが 1 件も解析されない場合に FormatException がスローされることを検証する
        /// </summary>
        [Fact]
        public void Parse_EmptyResult_ThrowsFormatException()
        {
            Action act = () => PageRangeParser.Parse(",", 10);

            var ex = Assert.Throws<FormatException>(act);
            Assert.Contains("空", ex.Message);
        }
    }
}
