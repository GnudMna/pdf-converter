using PdfConverter.Infrastructure;
using Xunit;

namespace PdfConverter.Tests.Infrastructure
{
    /// <summary>
    /// <see cref="ApplicationMetadata"/> の表示用メタデータを検証する
    /// </summary>
    public class ApplicationMetadataTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 同梱 OSS のクレジット一覧が空でないことを検証する
        /// </summary>
        [Fact]
        public void BundledComponents_ContainsPdfEngine()
        {
            Assert.NotEmpty(ApplicationMetadata.BundledComponents);
            Assert.Contains(
                ApplicationMetadata.BundledComponents,
                component => component.Name == "Docnet.Core");
            Assert.Contains(
                ApplicationMetadata.BundledComponents,
                component => component.Name == "PDFium");
        }

        /// <summary>
        /// About ダイアログ向けテキストに主要コンポーネントが含まれることを検証する
        /// </summary>
        [Fact]
        public void BundledComponentsText_IncludesLicenseNames()
        {
            string text = ApplicationMetadata.BundledComponentsText;

            Assert.Contains("Docnet.Core", text);
            Assert.Contains("PDFium", text);
            Assert.Contains("MIT", text);
            Assert.Contains("Apache 2.0", text);
        }
    }
}
