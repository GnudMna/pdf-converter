using System;
using FluentAssertions;
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

            result.Should().Equal(2);
        }

        /// <summary>
        /// 範囲指定 "2-4" が両端を含む 0 始まりインデックス [1,2,3] に変換されることを検証する
        /// </summary>
        [Fact]
        public void Parse_Range_ReturnsInclusiveZeroBasedIndexes()
        {
            var result = PageRangeParser.Parse("2-4", 10);

            result.Should().Equal(1, 2, 3);
        }

        /// <summary>
        /// 単一ページと範囲の混在入力で重複が排除され、昇順にソートされることを検証する
        /// </summary>
        [Fact]
        public void Parse_MixedInput_ReturnsSortedDistinctIndexes()
        {
            var result = PageRangeParser.Parse("1,3-5,3", 10);

            result.Should().Equal(0, 2, 3, 4);
        }

        /// <summary>
        /// カンマ区切りトークン前後の空白がトリムされて正しく解析されることを検証する
        /// </summary>
        [Fact]
        public void Parse_TrimsWhitespaceAroundTokens()
        {
            var result = PageRangeParser.Parse(" 1 , 2 ", 5);

            result.Should().Equal(0, 1);
        }

        /// <summary>
        /// ページ番号が 1 未満または総ページ数を超える場合に ArgumentOutOfRangeException がスローされることを検証する
        /// </summary>
        [Theory]
        [InlineData("0")]
        [InlineData("11")]
        public void Parse_PageOutOfRange_ThrowsArgumentOutOfRangeException(string pageRange)
        {
            Action act = () => PageRangeParser.Parse(pageRange, 10);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        /// <summary>
        /// 逆順・範囲外など不正な範囲指定で例外がスローされることを検証する
        /// </summary>
        [Theory]
        [InlineData("0-2")]
        [InlineData("5-2")]
        [InlineData("11-12")]
        public void Parse_InvalidRange_Throws(string pageRange)
        {
            Action act = () => PageRangeParser.Parse(pageRange, 10);

            act.Should().Throw<Exception>();
        }

        /// <summary>
        /// 数字以外のページ番号指定で FormatException がスローされることを検証する
        /// </summary>
        [Fact]
        public void Parse_NonNumericPage_ThrowsFormatException()
        {
            Action act = () => PageRangeParser.Parse("a", 10);

            act.Should().Throw<FormatException>()
                .WithMessage("*数字*");
        }

        /// <summary>
        /// 有効なページが 1 件も解析されない場合に ArgumentException がスローされることを検証する
        /// </summary>
        [Fact]
        public void Parse_EmptyResult_ThrowsArgumentException()
        {
            Action act = () => PageRangeParser.Parse(",", 10);

            act.Should().Throw<ArgumentException>()
                .WithMessage("*空*");
        }
    }
}
