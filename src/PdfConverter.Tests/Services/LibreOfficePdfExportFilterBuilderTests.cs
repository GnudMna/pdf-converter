using Moq;
using PdfConverter.Models;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="LibreOfficePdfExportFilterBuilder"/> の動作を検証する
    /// </summary>
    public class LibreOfficePdfExportFilterBuilderTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 既定設定ではシンプルな pdf 変換を指定することを検証する
        /// </summary>
        [Fact]
        public void BuildConvertToArgument_DefaultSettings_ReturnsPdf()
        {
            var settings = CreateSettings(
                WordToPdfPdfFormat.Standard,
                WordToPdfOptimizeFor.Print,
                exportBookmarks: true,
                exportComments: false);

            Assert.Equal("pdf", LibreOfficePdfExportFilterBuilder.BuildConvertToArgument(settings.Object));
        }

        /// <summary>
        /// PDF/A 形式では SelectPdfVersion を含むフィルタを指定することを検証する
        /// </summary>
        [Fact]
        public void BuildConvertToArgument_PdfA_IncludesSelectPdfVersion()
        {
            var settings = CreateSettings(
                WordToPdfPdfFormat.PdfA,
                WordToPdfOptimizeFor.Print,
                exportBookmarks: true,
                exportComments: false);

            string result = LibreOfficePdfExportFilterBuilder.BuildConvertToArgument(settings.Object);

            Assert.StartsWith("pdf:writer_pdf_Export:", result);
            Assert.Contains("\"SelectPdfVersion\":{\"type\":\"long\",\"value\":\"1\"}", result);
        }

        /// <summary>
        /// オンライン向け最適化では画像圧縮設定を含むことを検証する
        /// </summary>
        [Fact]
        public void BuildConvertToArgument_Online_IncludesCompressionSettings()
        {
            var settings = CreateSettings(
                WordToPdfPdfFormat.Standard,
                WordToPdfOptimizeFor.Online,
                exportBookmarks: true,
                exportComments: false);

            string result = LibreOfficePdfExportFilterBuilder.BuildConvertToArgument(settings.Object);

            Assert.Contains("\"ReduceImageResolution\":{\"type\":\"boolean\",\"value\":\"true\"}", result);
            Assert.Contains("\"Quality\":{\"type\":\"long\",\"value\":\"75\"}", result);
        }

        /// <summary>
        /// コメント出力オンでは ExportNotes を true にすることを検証する
        /// </summary>
        [Fact]
        public void BuildConvertToArgument_WithComments_EnablesExportNotes()
        {
            var settings = CreateSettings(
                WordToPdfPdfFormat.Standard,
                WordToPdfOptimizeFor.Print,
                exportBookmarks: true,
                exportComments: true);

            string result = LibreOfficePdfExportFilterBuilder.BuildConvertToArgument(settings.Object);

            Assert.Contains("\"ExportNotes\":{\"type\":\"boolean\",\"value\":\"true\"}", result);
        }

        /// <summary>
        /// しおり出力オフでは ExportBookmarks を false にすることを検証する
        /// </summary>
        [Fact]
        public void BuildConvertToArgument_NoBookmarks_DisablesExportBookmarks()
        {
            var settings = CreateSettings(
                WordToPdfPdfFormat.Standard,
                WordToPdfOptimizeFor.Print,
                exportBookmarks: false,
                exportComments: false);

            string result = LibreOfficePdfExportFilterBuilder.BuildConvertToArgument(settings.Object);

            Assert.Contains("\"ExportBookmarks\":{\"type\":\"boolean\",\"value\":\"false\"}", result);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        private static Mock<IWordToPdfConversionSettings> CreateSettings(
            WordToPdfPdfFormat pdfFormat,
            WordToPdfOptimizeFor optimizeFor,
            bool exportBookmarks,
            bool exportComments)
        {
            var settings = new Mock<IWordToPdfConversionSettings>();
            settings.SetupGet(s => s.PdfFormat).Returns(pdfFormat);
            settings.SetupGet(s => s.OptimizeFor).Returns(optimizeFor);
            settings.SetupGet(s => s.ExportBookmarks).Returns(exportBookmarks);
            settings.SetupGet(s => s.ExportComments).Returns(exportComments);
            return settings;
        }
    }
}
