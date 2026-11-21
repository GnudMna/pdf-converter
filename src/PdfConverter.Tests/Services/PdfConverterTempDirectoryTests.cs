using System;
using System.IO;
using PdfConverter.Services;
using PdfConverter.Tests.Helpers;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="PdfConverterTempDirectory"/> の一時ファイル管理を検証する
    /// </summary>
    [Collection(GlobalStateTestCollection.Name)]
    public sealed class PdfConverterTempDirectoryTests : IDisposable
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
        /// 一時 PDF パスがインスタンスディレクトリ配下に生成されることを検証する
        /// </summary>
        [Fact]
        public void CreateTempPdfPath_CreatesPathUnderInstanceDirectory()
        {
            string path = PdfConverterTempDirectory.CreateTempPdfPath();

            string instancePath = PdfConverterTempDirectory.GetInstancePath();
            Assert.Equal(instancePath, Path.GetDirectoryName(path), StringComparer.OrdinalIgnoreCase);
            Assert.StartsWith(_testRoot, instancePath, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(".pdf", path, StringComparison.OrdinalIgnoreCase);
            Assert.True(Directory.Exists(instancePath));
        }

        /// <summary>
        /// 一時作業ディレクトリがインスタンスディレクトリ配下に作成されることを検証する
        /// </summary>
        [Fact]
        public void CreateTempWorkDirectory_CreatesDirectoryUnderInstanceDirectory()
        {
            string path = PdfConverterTempDirectory.CreateTempWorkDirectory();

            Assert.Equal(
                PdfConverterTempDirectory.GetInstancePath(),
                Path.GetDirectoryName(path),
                StringComparer.OrdinalIgnoreCase);
            Assert.True(Directory.Exists(path));
        }

        /// <summary>
        /// インスタンスディレクトリ作成時にロックファイルを排他で保持することを検証する
        /// </summary>
        [Fact]
        public void EnsureInstanceDirectoryExists_HoldsExclusiveLockFile()
        {
            string instancePath = PdfConverterTempDirectory.EnsureInstanceDirectoryExists();
            string lockFilePath = Path.Combine(instancePath, PdfConverterTempDirectory.InstanceLockFileName);

            Assert.True(File.Exists(lockFilePath));
            Assert.ThrowsAny<IOException>(() =>
                new FileStream(lockFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None).Dispose());
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
        /// 他インスタンスがロック中の古いディレクトリは削除しないことを検証する
        /// </summary>
        [Fact]
        public void CleanupStaleEntries_SkipsDirectoryLockedByOtherInstance()
        {
            string otherInstancePath = Path.Combine(
                _testRoot,
                PdfConverterTempDirectory.InstanceDirectoryPrefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(otherInstancePath);
            string workFile = Path.Combine(otherInstancePath, "work.pdf");
            File.WriteAllText(workFile, "work");

            DateTime reference = DateTime.UtcNow;

            using (new FileStream(
                Path.Combine(otherInstancePath, PdfConverterTempDirectory.InstanceLockFileName),
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None))
            {
                Directory.SetLastWriteTimeUtc(otherInstancePath, reference.AddHours(-25));

                PdfConverterTempDirectory.CleanupStaleEntries(
                    PdfConverterTempDirectory.DefaultStaleAge,
                    reference);
            }

            Assert.True(File.Exists(workFile));
        }

        /// <summary>
        /// ロックが残っていない (終了済みインスタンスの) 古いディレクトリは削除することを検証する
        /// </summary>
        [Fact]
        public void CleanupStaleEntries_DeletesOldDirectoryWithReleasedLockFile()
        {
            string deadInstancePath = Path.Combine(
                _testRoot,
                PdfConverterTempDirectory.InstanceDirectoryPrefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(deadInstancePath);
            File.WriteAllText(Path.Combine(deadInstancePath, PdfConverterTempDirectory.InstanceLockFileName), string.Empty);

            DateTime reference = DateTime.UtcNow;
            Directory.SetLastWriteTimeUtc(deadInstancePath, reference.AddHours(-25));

            PdfConverterTempDirectory.CleanupStaleEntries(
                PdfConverterTempDirectory.DefaultStaleAge,
                reference);

            Assert.False(Directory.Exists(deadInstancePath));
        }

        /// <summary>
        /// 自インスタンスのディレクトリは古くても削除しないことを検証する
        /// </summary>
        [Fact]
        public void CleanupStaleEntries_SkipsOwnInstanceDirectory()
        {
            string tempPdfPath = PdfConverterTempDirectory.CreateTempPdfPath();
            File.WriteAllText(tempPdfPath, "pdf");

            DateTime reference = DateTime.UtcNow;
            Directory.SetLastWriteTimeUtc(PdfConverterTempDirectory.GetInstancePath(), reference.AddHours(-25));

            PdfConverterTempDirectory.CleanupStaleEntries(
                PdfConverterTempDirectory.DefaultStaleAge,
                reference);

            Assert.True(File.Exists(tempPdfPath));
        }

        /// <summary>
        /// 自インスタンスのディレクトリのみ削除し、他インスタンスのファイルは残すことを検証する
        /// </summary>
        [Fact]
        public void CleanupInstanceDirectory_RemovesOnlyOwnInstanceDirectory()
        {
            string ownPdfPath = PdfConverterTempDirectory.CreateTempPdfPath();
            File.WriteAllText(ownPdfPath, "own");
            string ownWorkDirectory = PdfConverterTempDirectory.CreateTempWorkDirectory();

            string otherInstancePath = Path.Combine(
                _testRoot,
                PdfConverterTempDirectory.InstanceDirectoryPrefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(otherInstancePath);
            string otherFile = Path.Combine(otherInstancePath, "other.pdf");
            File.WriteAllText(otherFile, "other");
            string rootFile = Path.Combine(_testRoot, "shared.pdf");
            File.WriteAllText(rootFile, "shared");

            PdfConverterTempDirectory.CleanupInstanceDirectory();

            Assert.False(Directory.Exists(PdfConverterTempDirectory.GetInstancePath()));
            Assert.False(File.Exists(ownPdfPath));
            Assert.False(Directory.Exists(ownWorkDirectory));
            Assert.True(File.Exists(otherFile));
            Assert.True(File.Exists(rootFile));
        }

        /// <summary>
        /// 削除後に再度一時ファイルを要求するとインスタンスディレクトリを作り直すことを検証する
        /// </summary>
        [Fact]
        public void CreateTempPdfPath_AfterCleanup_RecreatesInstanceDirectory()
        {
            PdfConverterTempDirectory.CreateTempPdfPath();
            PdfConverterTempDirectory.CleanupInstanceDirectory();

            string path = PdfConverterTempDirectory.CreateTempPdfPath();

            string instancePath = PdfConverterTempDirectory.GetInstancePath();
            Assert.True(Directory.Exists(instancePath));
            Assert.True(File.Exists(Path.Combine(instancePath, PdfConverterTempDirectory.InstanceLockFileName)));
            Assert.Equal(instancePath, Path.GetDirectoryName(path), StringComparer.OrdinalIgnoreCase);
        }


        /********************************************************************************/
        /*                              IDisposable                                   */
        /********************************************************************************/
        /// <inheritdoc/>
        public void Dispose()
        {
            PdfConverterTempDirectory.CleanupInstanceDirectory();
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
