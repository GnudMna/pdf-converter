using FluentAssertions;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="PdfFileHelper"/> の動作を検証する
    /// </summary>
    public class PdfFileHelperTests
    {
        /// <summary>
        /// PDF 拡張子を正しく判定することを検証する
        /// </summary>
        [Theory]
        [InlineData("C:\\docs\\sample.pdf", true)]
        [InlineData("C:\\docs\\sample.PDF", true)]
        [InlineData("C:\\docs\\sample.png", false)]
        [InlineData(null, false)]
        public void IsPdfFile_DetectsExtension(string path, bool expected)
        {
            PdfFileHelper.IsPdfFile(path).Should().Be(expected);
        }
    }
}
