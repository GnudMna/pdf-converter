using PdfConverter.Models;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="WordToPdfBackendParser"/> の動作を検証する
    /// </summary>
    public class WordToPdfBackendParserTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 設定文字列を正しく変換することを検証する
        /// </summary>
        [Theory]
        [InlineData("MicrosoftWord", WordToPdfBackend.MicrosoftWord)]
        [InlineData("LibreOffice", WordToPdfBackend.LibreOffice)]
        [InlineData(null, WordToPdfBackend.MicrosoftWord)]
        [InlineData("", WordToPdfBackend.MicrosoftWord)]
        [InlineData("Unknown", WordToPdfBackend.MicrosoftWord)]
        public void Parse_ConvertsKnownValues(string value, WordToPdfBackend expected)
        {
            Assert.Equal(expected, WordToPdfBackendParser.Parse(value));
        }
    }
}
