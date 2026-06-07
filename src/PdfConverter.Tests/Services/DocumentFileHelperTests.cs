using FluentAssertions;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="DocumentFileHelper"/> の動作を検証する
    /// </summary>
    public class DocumentFileHelperTests
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
            DocumentFileHelper.IsPdfFile(path).Should().Be(expected);
        }

        /// <summary>
        /// Word 拡張子を正しく判定することを検証する
        /// </summary>
        [Theory]
        [InlineData("C:\\docs\\sample.doc", true)]
        [InlineData("C:\\docs\\sample.docx", true)]
        [InlineData("C:\\docs\\sample.DOCX", true)]
        [InlineData("C:\\docs\\sample.pdf", false)]
        public void IsWordFile_DetectsExtension(string path, bool expected)
        {
            DocumentFileHelper.IsWordFile(path).Should().Be(expected);
        }

        /// <summary>
        /// サポート対象の拡張子を正しく判定することを検証する
        /// </summary>
        [Theory]
        [InlineData("C:\\docs\\sample.pdf", true)]
        [InlineData("C:\\docs\\sample.docx", true)]
        [InlineData("C:\\docs\\sample.txt", false)]
        public void IsSupportedDocument_DetectsExtension(string path, bool expected)
        {
            DocumentFileHelper.IsSupportedDocument(path).Should().Be(expected);
        }
    }
}
