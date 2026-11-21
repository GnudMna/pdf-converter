using System;
using System.IO;
using PdfConverter.Infrastructure;
using Xunit;

namespace PdfConverter.Tests.Infrastructure
{
    /// <summary>
    /// <see cref="ApplicationStartupHelper"/> の起動補助ロジックを検証する
    /// </summary>
    public class ApplicationStartupHelperTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 起動引数に有効な PDF パスがある場合に初期ドキュメントパスを返すことを検証する
        /// </summary>
        [Fact]
        public void TryGetInitialDocumentPath_WithSupportedPdf_ReturnsTrue()
        {
            string pdfPath = CreateTempPdfPath();

            try
            {
                bool found = ApplicationStartupHelper.TryGetInitialDocumentPath(
                    new[] { pdfPath },
                    out string filePath);

                Assert.True(found);
                Assert.Equal(pdfPath, filePath);
            }
            finally
            {
                File.Delete(pdfPath);
            }
        }

        /// <summary>
        /// 起動引数が空の場合は初期ドキュメントパスを返さないことを検証する
        /// </summary>
        [Fact]
        public void TryGetInitialDocumentPath_WithEmptyArgs_ReturnsFalse()
        {
            bool found = ApplicationStartupHelper.TryGetInitialDocumentPath(
                Array.Empty<string>(),
                out string filePath);

            Assert.False(found);
            Assert.Null(filePath);
        }

        /// <summary>
        /// 存在しないファイルのみが渡された場合は初期ドキュメントパスを返さないことを検証する
        /// </summary>
        [Fact]
        public void TryGetInitialDocumentPath_WithMissingFile_ReturnsFalse()
        {
            string missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");

            bool found = ApplicationStartupHelper.TryGetInitialDocumentPath(
                new[] { missingPath },
                out string filePath);

            Assert.False(found);
            Assert.Null(filePath);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        private static string CreateTempPdfPath()
        {
            string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
            File.WriteAllBytes(path, new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D });
            return path;
        }
    }
}
