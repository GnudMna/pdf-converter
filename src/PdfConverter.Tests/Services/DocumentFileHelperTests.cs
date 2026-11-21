using System;
using System.IO;
using System.Windows;
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
        /// コマンドライン引数から先頭のサポート対象ドキュメントを取得することを検証する
        /// </summary>
        [Fact]
        public void TryGetFirstSupportedPathFromArgs_FirstSupportedPath_ReturnsTrue()
        {
            string pdfPath = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.pdf");
            string docxPath = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.docx");
            File.WriteAllText(pdfPath, "placeholder");
            File.WriteAllText(docxPath, "placeholder");

            try
            {
                bool found = DocumentFileHelper.TryGetFirstSupportedPathFromArgs(
                    new[] { "ignored.txt", pdfPath, docxPath },
                    out string filePath);

                Assert.True(found);
                Assert.Equal(Path.GetFullPath(pdfPath), filePath);
            }
            finally
            {
                File.Delete(pdfPath);
                File.Delete(docxPath);
            }
        }

        /// <summary>
        /// 引用符付きのコマンドライン引数を正しく解釈することを検証する
        /// </summary>
        [Fact]
        public void TryGetFirstSupportedPathFromArgs_QuotedPath_ReturnsTrue()
        {
            string pdfPath = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.pdf");
            File.WriteAllText(pdfPath, "placeholder");

            try
            {
                bool found = DocumentFileHelper.TryGetFirstSupportedPathFromArgs(
                    new[] { "\"" + pdfPath + "\"" },
                    out string filePath);

                Assert.True(found);
                Assert.Equal(Path.GetFullPath(pdfPath), filePath);
            }
            finally
            {
                File.Delete(pdfPath);
            }
        }

        /// <summary>
        /// サポート対象がない引数一覧では false を返すことを検証する
        /// </summary>
        [Fact]
        public void TryGetFirstSupportedPathFromArgs_NullArgs_ReturnsFalse()
        {
            bool found = DocumentFileHelper.TryGetFirstSupportedPathFromArgs(null, out string filePath);

            Assert.False(found);
            Assert.Null(filePath);
        }

        /// <summary>
        /// 空の引数一覧では false を返すことを検証する
        /// </summary>
        [Fact]
        public void TryGetFirstSupportedPathFromArgs_EmptyArgs_ReturnsFalse()
        {
            bool found = DocumentFileHelper.TryGetFirstSupportedPathFromArgs(Array.Empty<string>(), out string filePath);

            Assert.False(found);
            Assert.Null(filePath);
        }

        /// <summary>
        /// 非対応拡張子のみの引数一覧では false を返すことを検証する
        /// </summary>
        [Fact]
        public void TryGetFirstSupportedPathFromArgs_NoSupportedPath_ReturnsFalse()
        {
            bool found = DocumentFileHelper.TryGetFirstSupportedPathFromArgs(
                new[] { "sample.txt", "--help" },
                out string filePath);

            Assert.False(found);
            Assert.Null(filePath);
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
            Assert.Equal(Path.GetFullPath(path), ex.FileName);
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

        /// <summary>
        /// 引用符付きの Word ファイルパスを受け入れることを検証する
        /// </summary>
        [Fact]
        public void ValidateWordFilePath_QuotedPath_DoesNotThrow()
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.docx");
            File.WriteAllText(path, "placeholder");

            try
            {
                Action act = () => DocumentFileHelper.ValidateWordFilePath("\"" + path + "\"");

                act();
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// 有効なパスを正規化して返すことを検証する
        /// </summary>
        [Fact]
        public void TryResolveSupportedDocumentPath_ValidPdf_ReturnsFullPath()
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.pdf");
            File.WriteAllText(path, "placeholder");

            try
            {
                bool resolved = DocumentFileHelper.TryResolveSupportedDocumentPath(path, out string normalizedPath);

                Assert.True(resolved);
                Assert.Equal(Path.GetFullPath(path), normalizedPath);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// 存在しないファイルは FileNotFound として解決できないことを検証する
        /// </summary>
        [Fact]
        public void ResolveSupportedDocumentPath_MissingFile_ReturnsFileNotFound()
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.pdf");

            SupportedDocumentPathError error = DocumentFileHelper.ResolveSupportedDocumentPath(path, out _);

            Assert.Equal(SupportedDocumentPathError.FileNotFound, error);
            Assert.Equal(DocumentFileHelper.FileNotFoundMessage, DocumentFileHelper.GetSupportedDocumentPathErrorMessage(error));
        }

        /// <summary>
        /// 非対応拡張子は UnsupportedExtension として解決できないことを検証する
        /// </summary>
        [Fact]
        public void ResolveSupportedDocumentPath_UnsupportedExtension_ReturnsUnsupportedExtension()
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.txt");
            File.WriteAllText(path, "placeholder");

            try
            {
                SupportedDocumentPathError error = DocumentFileHelper.ResolveSupportedDocumentPath(path, out _);

                Assert.Equal(SupportedDocumentPathError.UnsupportedExtension, error);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// ValidateAndNormalizeSupportedDocumentPath が空パスを ArgumentException として拒否することを検証する
        /// </summary>
        [Fact]
        public void ValidateAndNormalizeSupportedDocumentPath_EmptyPath_ThrowsArgumentException()
        {
            Action act = () => DocumentFileHelper.ValidateAndNormalizeSupportedDocumentPath("   ");

            var ex = Assert.Throws<ArgumentException>(act);
            Assert.Contains("入力ファイルのパスが指定されていません。", ex.Message);
        }

        /// <summary>
        /// コマンドライン引数で存在しないファイルをスキップし、次の有効なパスを返すことを検証する
        /// </summary>
        [Fact]
        public void TryGetFirstSupportedPathFromArgs_SkipsMissingFile_ReturnsExistingPath()
        {
            string missingPath = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.pdf");
            string existingPath = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.pdf");
            File.WriteAllText(existingPath, "placeholder");

            try
            {
                bool found = DocumentFileHelper.TryGetFirstSupportedPathFromArgs(
                    new[] { missingPath, existingPath },
                    out string filePath);

                Assert.True(found);
                Assert.Equal(Path.GetFullPath(existingPath), filePath);
            }
            finally
            {
                File.Delete(existingPath);
            }
        }

        /// <summary>
        /// ドラッグデータから存在するサポート対象ファイルの正規化済みパスを取得することを検証する
        /// </summary>
        [Fact]
        public void TryGetFirstSupportedPath_ExistingFile_ReturnsNormalizedPath()
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.pdf");
            File.WriteAllText(path, "placeholder");

            try
            {
                var data = new DataObject();
                data.SetData(DataFormats.FileDrop, new[] { path });

                bool found = DocumentFileHelper.TryGetFirstSupportedPath(data, out string filePath);

                Assert.True(found);
                Assert.Equal(Path.GetFullPath(path), filePath);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// ドラッグデータに存在しないファイルのみ含まれる場合は false を返すことを検証する
        /// </summary>
        [Fact]
        public void TryGetFirstSupportedPath_MissingFile_ReturnsFalse()
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.pdf");
            var data = new DataObject();
            data.SetData(DataFormats.FileDrop, new[] { path });

            bool found = DocumentFileHelper.TryGetFirstSupportedPath(data, out string filePath);

            Assert.False(found);
            Assert.Null(filePath);
        }

        /// <summary>
        /// 存在しないファイルを含むドラッグデータでは ContainsSupportedDocument が false になることを検証する
        /// </summary>
        [Fact]
        public void ContainsSupportedDocument_MissingFile_ReturnsFalse()
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.pdf");
            var data = new DataObject();
            data.SetData(DataFormats.FileDrop, new[] { path });

            Assert.False(DocumentFileHelper.ContainsSupportedDocument(data));
        }
    }
}
