using FluentAssertions;
using PdfConverter.Models;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="OutputImageFormatParser"/> の動作を検証する
    /// </summary>
    public class OutputImageFormatParserTests
    {
        /// <summary>
        /// 既知の文字列が正しく変換されることを検証する
        /// </summary>
        [Theory]
        [InlineData("Png", OutputImageFormat.Png)]
        [InlineData("Jpeg", OutputImageFormat.Jpeg)]
        [InlineData("Bmp", OutputImageFormat.Bmp)]
        [InlineData(null, OutputImageFormat.Png)]
        [InlineData("", OutputImageFormat.Png)]
        [InlineData("Unknown", OutputImageFormat.Png)]
        public void Parse_ConvertsKnownValues(string value, OutputImageFormat expected)
        {
            OutputImageFormatParser.Parse(value).Should().Be(expected);
        }
    }
}
