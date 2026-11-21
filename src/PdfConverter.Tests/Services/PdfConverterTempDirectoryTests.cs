using System;
using System.IO;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="PdfConverterTempDirectory"/> の一時ファイル管理を検証する
    /// </summary>
    public class PdfConverterTempDirectoryTests : IDisposable
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        private readonly string _testRoot;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        public PdfConverterTempDirectoryTests()
        {
            _testRoot = Path.Combine(
                Path.GetTempPath(),
                "PdfConverter-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testRoot);
            PdfConverterTempDirectory.RootPathProvider = () => _testRoot;
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// ルートパスが <c>%TEMP%\PdfConverter</c> 配下になることを検証する
        /// </summary>
        [Fact]
        public void GetRootPath_WithoutProvider_ReturnsTempSubfolder()
        {
            PdfConverterTempDirectory.RootPathProvider = null;

            string path = PdfConverterTempDirectory.GetRootPath();

            Assert.Equal(Path.Combine(Path.GetTempPath(), PdfConverterTempDirectory.FolderName), path);
            PdfConverterTempDirectory.RootPathProvider = () => _testRoot;
        }

        /// <summary>
        /// 一時 PDF パスがルート配下に生成されることを検証する
        /// </summary>
        [Fact]
        public void CreateTempPdfPath_CreatesPathUnderRoot()
        {
            string path = PdfConverterTempDirectory.CreateTempPdfPath();

            Assert.StartsWith(_testRoot, path, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(".pdf", path, StringComparison.OrdinalIgnoreCase);
            Assert.True(Directory.Exists(_testRoot));
        }

        /// <summary>
        /// 古いファイルのみ削除し、新しいファイルは残すことを検証する
        /// </summary>
        [Fact]
        public void CleanupStaleEntries_DeletesOnlyOldFiles()
        {
            string oldFile = Path.Combine(_testRoot, "old.pdf");
            string newFile = Path.Combine(_testRoot, "new.pdf");
            Directory.CreateDirectory(_testRoot);
            File.WriteAllText(oldFile, "old");
            File.WriteAllText(newFile, "new");

            DateTime reference = DateTime.UtcNow;
            File.SetLastWriteTimeUtc(oldFile, reference.AddHours(-25));
            File.SetLastWriteTimeUtc(newFile, reference.AddMinutes(-5));

            PdfConverterTempDirectory.CleanupStaleEntries(
                PdfConverterTempDirectory.DefaultStaleAge,
                reference);

            Assert.False(File.Exists(oldFile));
            Assert.True(File.Exists(newFile));
        }

        /// <summary>
        /// 古い作業ディレクトリを削除することを検証する
        /// </summary>
        [Fact]
        public void CleanupStaleEntries_DeletesOldDirectories()
        {
            string oldDirectory = Path.Combine(_testRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(oldDirectory);
            File.WriteAllText(Path.Combine(oldDirectory, "work.pdf"), "work");

            DateTime reference = DateTime.UtcNow;
            Directory.SetLastWriteTimeUtc(oldDirectory, reference.AddHours(-25));

            PdfConverterTempDirectory.CleanupStaleEntries(
                PdfConverterTempDirectory.DefaultStaleAge,
                reference);

            Assert.False(Directory.Exists(oldDirectory));
        }

        /// <summary>
        /// ルート配下のファイルとディレクトリをベストエフォートで削除することを検証する
        /// </summary>
        [Fact]
        public void TryCleanupAll_RemovesFilesAndDirectories()
        {
            string filePath = Path.Combine(_testRoot, "orphan.pdf");
            string directoryPath = Path.Combine(_testRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testRoot);
            Directory.CreateDirectory(directoryPath);
            File.WriteAllText(filePath, "orphan");
            File.WriteAllText(Path.Combine(directoryPath, "nested.pdf"), "nested");

            PdfConverterTempDirectory.TryCleanupAll();

            Assert.False(File.Exists(filePath));
            Assert.False(Directory.Exists(directoryPath));
        }


        /********************************************************************************/
        /*                              IDisposable                                   */
        /********************************************************************************/
        /// <inheritdoc/>
        public void Dispose()
        {
            PdfConverterTempDirectory.RootPathProvider = null;

            try
            {
                if (Directory.Exists(_testRoot))
                {
                    Directory.Delete(_testRoot, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
