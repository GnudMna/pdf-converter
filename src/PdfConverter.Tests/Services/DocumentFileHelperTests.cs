using System;
using System.IO;
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

        /// <summary>
        /// 空のパスを ArgumentException として拒否することを検証する
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ValidateWordFilePath_EmptyPath_ThrowsArgumentException(string path)
        {
            Action act = () => DocumentFileHelper.ValidateWordFilePath(path);

            act.Should().Throw<ArgumentException>()
                .WithMessage("Word ファイルのパスが指定されていません。*")
                .And.ParamName.Should().Be("wordFilePath");
        }

        /// <summary>
        /// 存在しないファイルを FileNotFoundException として拒否することを検証する
        /// </summary>
        [Fact]
        public void ValidateWordFilePath_MissingFile_ThrowsFileNotFoundException()
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.docx");

            Action act = () => DocumentFileHelper.ValidateWordFilePath(path);

            act.Should().Throw<FileNotFoundException>()
                .WithMessage("Word ファイルが見つかりません。*")
                .Which.FileName.Should().Be(path);
        }

        /// <summary>
        /// Word 以外の拡張子を ArgumentException として拒否することを検証する
        /// </summary>
        [Fact]
        public void ValidateWordFilePath_NonWordExtension_ThrowsArgumentException()
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.pdf");
            File.WriteAllText(path, "placeholder");

            try
            {
                Action act = () => DocumentFileHelper.ValidateWordFilePath(path);

                act.Should().Throw<ArgumentException>()
                    .WithMessage("Word ファイル (.doc / .docx) を指定してください。*")
                    .And.ParamName.Should().Be("wordFilePath");
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// 有効な Word ファイルパスを受け入れることを検証する
        /// </summary>
        [Fact]
        public void ValidateWordFilePath_ValidWordFile_DoesNotThrow()
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.docx");
            File.WriteAllText(path, "placeholder");

            try
            {
                Action act = () => DocumentFileHelper.ValidateWordFilePath(path);

                act.Should().NotThrow();
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
