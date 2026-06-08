using FluentAssertions;
using PdfConverter.Models;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="ResolutionModeParser"/> の動作を検証する
    /// </summary>
    public class ResolutionModeParserTests
    {
        /// <summary>
        /// 既知の文字列が正しく変換されることを検証する
        /// </summary>
        [Theory]
        [InlineData("Width", ResolutionMode.Width)]
        [InlineData("Height", ResolutionMode.Height)]
        [InlineData("Dpi", ResolutionMode.Dpi)]
        [InlineData(null, ResolutionMode.Width)]
        [InlineData("", ResolutionMode.Width)]
        [InlineData("Default", ResolutionMode.Width)]
        [InlineData("Unknown", ResolutionMode.Width)]
        public void Parse_ConvertsKnownValues(string value, ResolutionMode expected)
        {
            ResolutionModeParser.Parse(value).Should().Be(expected);
        }
    }
}
