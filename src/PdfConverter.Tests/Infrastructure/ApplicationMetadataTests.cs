using System;
using System.IO;
using System.Linq;
using System.Text.Json;
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
                component => component.Name == "PDFiumCore");
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

            Assert.Contains("PDFiumCore", text);
            Assert.Contains("PDFium", text);
            Assert.Contains("MIT", text);
            Assert.Contains("Apache 2.0", text);
        }

        /// <summary>
        /// 同梱 OSS 一覧の NuGet パッケージが、アプリ本体の依存パッケージ (deps.json) と名前・バージョンとも一致することを検証する
        /// </summary>
        /// <remarks>
        /// PDFium のネイティブ DLL は bblanchon.PDFium.Win32 パッケージで配布されるため、一覧の "PDFium" と対応付ける
        /// </remarks>
        [Fact]
        public void BundledComponents_MatchApplicationPackageDependencies()
        {
            string depsPath = Path.Combine(AppContext.BaseDirectory, "PDF Converter.deps.json");
            using (JsonDocument deps = JsonDocument.Parse(File.ReadAllText(depsPath)))
            {
                string[] expected = deps.RootElement.GetProperty("libraries").EnumerateObject()
                    .Where(library => library.Value.GetProperty("type").GetString() == "package")
                    .Select(library => library.Name.Replace("bblanchon.PDFium.Win32/", "PDFium/"))
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();

                string[] actual = ApplicationMetadata.BundledComponents
                    .Where(component => component.Name != ApplicationMetadata.DotNetRuntimeComponentName)
                    .Select(component => $"{component.Name}/{component.Version}")
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();

                Assert.Equal(expected, actual);
            }
        }

        /// <summary>
        /// 同梱 OSS 一覧に、実行中の .NET ランタイムのバージョンが含まれることを検証する
        /// </summary>
        [Fact]
        public void BundledComponents_IncludesDotNetRuntime()
        {
            Assert.Contains(
                ApplicationMetadata.BundledComponents,
                component => component.Name == ApplicationMetadata.DotNetRuntimeComponentName
                    && component.Version == Environment.Version.ToString());
        }

        /// <summary>
        /// 外部ソフトウェアの説明に、同梱済みのランタイムが含まれないことを検証する
        /// </summary>
        [Fact]
        public void ExternalDependenciesText_ExcludesBundledRuntime()
        {
            string text = ApplicationMetadata.ExternalDependenciesText;

            Assert.Contains("Microsoft Word", text);
            Assert.Contains("LibreOffice", text);
            Assert.DoesNotContain(".NET", text);
        }

        /// <summary>
        /// 利用者向けドキュメントの表紙が、実行ファイルと同じフォルダーの docs\index.html を指すことを検証する
        /// </summary>
        /// <remarks>
        /// インストーラー (installer/PdfConverter.iss) のスタートメニューのショートカットと同じ場所を指す
        /// </remarks>
        [Fact]
        public void UserDocsIndexFilePath_PointsToDocsIndexUnderBaseDirectory()
        {
            string expected = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docs", "index.html");

            Assert.Equal(expected, ApplicationMetadata.UserDocsIndexFilePath);
        }

        /// <summary>
        /// 本アプリの使用許諾が、実行ファイルと同じフォルダーの LICENSE.txt を指すことを検証する
        /// </summary>
        /// <remarks>
        /// インストーラー (installer/PdfConverter.iss) がリポジトリの LICENSE を置く場所と同じ
        /// </remarks>
        [Fact]
        public void LicenseFilePath_PointsToLicenseUnderBaseDirectory()
        {
            string expected = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LICENSE.txt");

            Assert.Equal(expected, ApplicationMetadata.LicenseFilePath);
        }
    }
}
