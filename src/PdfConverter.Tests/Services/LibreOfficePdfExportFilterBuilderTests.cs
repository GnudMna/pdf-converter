using FluentAssertions;
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

            LibreOfficePdfExportFilterBuilder.BuildConvertToArgument(settings.Object)
                .Should().Be("pdf");
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

            result.Should().StartWith("pdf:writer_pdf_Export:");
            result.Should().Contain("\"SelectPdfVersion\":{\"type\":\"long\",\"value\":\"1\"}");
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

            result.Should().Contain("\"ReduceImageResolution\":{\"type\":\"boolean\",\"value\":\"true\"}");
            result.Should().Contain("\"Quality\":{\"type\":\"long\",\"value\":\"75\"}");
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

            result.Should().Contain("\"ExportNotes\":{\"type\":\"boolean\",\"value\":\"true\"}");
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

            result.Should().Contain("\"ExportBookmarks\":{\"type\":\"boolean\",\"value\":\"false\"}");
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
