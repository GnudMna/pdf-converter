using FluentAssertions;
using PdfConverter.Models;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="WordToPdfOptimizeForParser"/> の動作を検証する
    /// </summary>
    public class WordToPdfOptimizeForParserTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 既知の文字列を出力最適化モードに変換することを検証する
        /// </summary>
        [Theory]
        [InlineData("Print", WordToPdfOptimizeFor.Print)]
        [InlineData("Online", WordToPdfOptimizeFor.Online)]
        [InlineData(null, WordToPdfOptimizeFor.Print)]
        [InlineData("", WordToPdfOptimizeFor.Print)]
        [InlineData("Unknown", WordToPdfOptimizeFor.Print)]
        public void Parse_ConvertsKnownValues(string value, WordToPdfOptimizeFor expected)
        {
            WordToPdfOptimizeForParser.Parse(value).Should().Be(expected);
        }
    }
}
