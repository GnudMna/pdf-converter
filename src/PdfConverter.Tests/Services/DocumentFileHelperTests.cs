using System;
using System.IO;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="DocumentFileHelper"/> の動作を検証する
    /// </summary>
    public class DocumentFileHelperTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
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
            Assert.Equal(expected, DocumentFileHelper.IsPdfFile(path));
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
            Assert.Equal(expected, DocumentFileHelper.IsWordFile(path));
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
            Assert.Equal(expected, DocumentFileHelper.IsSupportedDocument(path));
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

            var ex = Assert.Throws<ArgumentException>(act);
            Assert.Contains("Word ファイルのパスが指定されていません。", ex.Message);
            Assert.Equal("wordFilePath", ex.ParamName);
        }

        /// <summary>
        /// 存在しないファイルを FileNotFoundException として拒否することを検証する
        /// </summary>
        [Fact]
        public void ValidateWordFilePath_MissingFile_ThrowsFileNotFoundException()
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.docx");

            Action act = () => DocumentFileHelper.ValidateWordFilePath(path);

            var ex = Assert.Throws<FileNotFoundException>(act);
            Assert.Contains("Word ファイルが見つかりません。", ex.Message);
            Assert.Equal(path, ex.FileName);
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

                var ex = Assert.Throws<ArgumentException>(act);
                Assert.Contains("Word ファイル (.doc / .docx) を指定してください。", ex.Message);
                Assert.Equal("wordFilePath", ex.ParamName);
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

                act();
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
