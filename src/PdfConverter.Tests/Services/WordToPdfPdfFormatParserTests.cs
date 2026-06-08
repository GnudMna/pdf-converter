using FluentAssertions;
using PdfConverter.Models;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="WordToPdfPdfFormatParser"/> の動作を検証する
    /// </summary>
    public class WordToPdfPdfFormatParserTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 既知の文字列を PDF 形式に変換することを検証する
        /// </summary>
        [Theory]
        [InlineData("Standard", WordToPdfPdfFormat.Standard)]
        [InlineData("PdfA", WordToPdfPdfFormat.PdfA)]
        [InlineData(null, WordToPdfPdfFormat.Standard)]
        [InlineData("", WordToPdfPdfFormat.Standard)]
        [InlineData("Unknown", WordToPdfPdfFormat.Standard)]
        public void Parse_ConvertsKnownValues(string value, WordToPdfPdfFormat expected)
        {
            WordToPdfPdfFormatParser.Parse(value).Should().Be(expected);
        }
    }
}
