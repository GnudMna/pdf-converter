using System;
using System.Collections.Generic;
using System.IO;
using PdfConverter.Infrastructure;
using PdfConverter.Services;
using PdfConverter.Tests.Helpers;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="PortableUpdatePackage"/> の zip の展開とマニフェストの読み込みを検証する
    /// </summary>
    public sealed class PortableUpdatePackageTests : IDisposable
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>テスト用のルート</summary>
        private readonly string _root;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// テスト用のフォルダーを用意する
        /// </summary>
        public PortableUpdatePackageTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "PdfConverter-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// テスト用のフォルダーを削除する
        /// </summary>
        public void Dispose()
        {
            PdfTestAssetFactory.DeleteDirectoryIfExists(_root);
        }

        /// <summary>
        /// ポータブル版の zip をサブフォルダーも含めて展開することを検証する
        /// </summary>
        [Fact]
        public void Extract_ValidZip_ExtractsFiles()
        {
            string zipPath = WriteZip(PortablePackageTestHelper.BuildZip(
                PortablePackageTestHelper.CreateAppFiles("2", (@"docs\index.html", "doc"))));
            string destination = Path.Combine(_root, "out");

            PortableUpdatePackage.Extract(zipPath, destination);

            Assert.Equal("exe 2", File.ReadAllText(Path.Combine(destination, PortableUpdatePackage.AppExecutableFileName)));
            Assert.Equal("doc", File.ReadAllText(Path.Combine(destination, "docs", "index.html")));
            Assert.True(File.Exists(Path.Combine(destination, PortableMode.ManifestFileName)));
        }

        /// <summary>
        /// 展開先の外を指すエントリーを含む zip を拒否することを検証する
        /// </summary>
        [Fact]
        public void Extract_EntryOutsideDestination_Throws()
        {
            Dictionary<string, string> files = PortablePackageTestHelper.CreateAppFiles("2");
            files[PortableMode.ManifestFileName] = PortableUpdatePackage.AppExecutableFileName;
            files["../evil.txt"] = "evil";
            string zipPath = WriteZip(PortablePackageTestHelper.BuildRawZip(files));
            string destination = Path.Combine(_root, "out");

            Assert.Throws<InvalidDataException>(() => PortableUpdatePackage.Extract(zipPath, destination));
            Assert.False(File.Exists(Path.Combine(_root, "evil.txt")));
        }

        /// <summary>
        /// マーカーファイルの無い zip (ポータブル版ではない) を拒否することを検証する
        /// </summary>
        [Fact]
        public void Extract_MarkerMissing_Throws()
        {
            Dictionary<string, string> files = PortablePackageTestHelper.CreateAppFiles("2");
            files.Remove(PortableMode.MarkerFileName);
            string zipPath = WriteZip(PortablePackageTestHelper.BuildZip(files));

            InvalidDataException ex = Assert.Throws<InvalidDataException>(
                () => PortableUpdatePackage.Extract(zipPath, Path.Combine(_root, "out")));
            Assert.Contains(PortableMode.MarkerFileName, ex.Message);
        }

        /// <summary>
        /// マニフェストの空行と重複を除き、区切り文字をそろえて読み込むことを検証する
        /// </summary>
        [Fact]
        public void ReadManifest_ValidManifest_ReturnsDistinctPaths()
        {
            File.WriteAllText(
                Path.Combine(_root, PortableMode.ManifestFileName),
                "PDF Converter.exe\r\n\r\ndocs/index.html\r\nDOCS\\index.html\r\n");

            IReadOnlyList<string> paths = PortableUpdatePackage.ReadManifest(_root);

            Assert.Equal(new[] { "PDF Converter.exe", @"docs\index.html" }, paths);
        }

        /// <summary>
        /// 利用者のデータ・アプリフォルダーの外・絶対パスを指すマニフェストを拒否することを検証する
        /// </summary>
        [Theory]
        [InlineData(@"UserData\user.config")]
        [InlineData(@"..\outside.dll")]
        [InlineData(@"docs\..\..\outside.dll")]
        [InlineData(@"C:\Windows\notepad.exe")]
        [InlineData(@".update-backup-abc\old.dll")]
        [InlineData(@"docs\")]
        public void ReadManifest_UnsafePath_Throws(string path)
        {
            File.WriteAllText(Path.Combine(_root, PortableMode.ManifestFileName), path);

            Assert.Throws<InvalidDataException>(() => PortableUpdatePackage.ReadManifest(_root));
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>zip をテスト用のフォルダーへ書き出す</summary>
        private string WriteZip(byte[] bytes)
        {
            string path = Path.Combine(_root, "package.zip");
            File.WriteAllBytes(path, bytes);
            return path;
        }
    }
}
