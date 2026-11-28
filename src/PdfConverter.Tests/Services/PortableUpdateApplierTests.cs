using System;
using System.IO;
using PdfConverter.Infrastructure;
using PdfConverter.Services;
using PdfConverter.Tests.Helpers;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="PortableUpdateApplier"/> のファイルの置き換えと取り消しを検証する
    /// </summary>
    [Collection(GlobalStateTestCollection.Name)]
    public sealed class PortableUpdateApplierTests : IDisposable
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>テスト用のルート</summary>
        private readonly string _root;

        /// <summary>新しい版を展開したフォルダー</summary>
        private readonly string _source;

        /// <summary>置き換えるアプリフォルダー</summary>
        private readonly string _target;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// テスト用のフォルダーを用意し、ログの出力先を切り替える
        /// </summary>
        public PortableUpdateApplierTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "PdfConverter-tests", Guid.NewGuid().ToString("N"));
            _source = Path.Combine(_root, "source");
            _target = Path.Combine(_root, "target");
            Directory.CreateDirectory(_source);
            Directory.CreateDirectory(_target);
            ApplicationLogger.LogDirectory = Path.Combine(_root, "logs");
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// ログの出力先を戻し、テスト用のフォルダーを削除する
        /// </summary>
        public void Dispose()
        {
            ApplicationLogger.LogDirectory = null;
            PdfTestAssetFactory.DeleteDirectoryIfExists(_root);
        }

        /// <summary>
        /// 新しい版のファイルに置き換え、旧版にしか無いファイルと空になったフォルダーを消し、利用者のデータは残すことを検証する
        /// </summary>
        [Fact]
        public void TryApply_ValidPackage_ReplacesFilesAndKeepsUserData()
        {
            PortablePackageTestHelper.WriteAppDirectory(
                _target,
                PortablePackageTestHelper.CreateAppFiles("1", ("lib.dll", "old lib"), (@"docs\old.html", "old doc")));
            WriteFile(_target, @"UserData\user.config", "settings");
            WriteFile(_target, "notes.txt", "user file");
            PortablePackageTestHelper.WriteAppDirectory(
                _source,
                PortablePackageTestHelper.CreateAppFiles("2", (@"docs\new.html", "new doc")));

            bool applied = PortableUpdateApplier.TryApply(_source, _target, out string errorMessage);

            Assert.True(applied, errorMessage);
            Assert.Equal("exe 2", ReadFile(_target, PortableUpdatePackage.AppExecutableFileName));
            Assert.Equal("new doc", ReadFile(_target, @"docs\new.html"));
            Assert.False(File.Exists(Path.Combine(_target, "lib.dll")));
            Assert.False(File.Exists(Path.Combine(_target, @"docs\old.html")));
            Assert.Equal("settings", ReadFile(_target, @"UserData\user.config"));
            Assert.Equal("user file", ReadFile(_target, "notes.txt"));
            Assert.Empty(Directory.GetDirectories(_target, PortableUpdateApplier.BackupDirectoryPrefix + "*"));
        }

        /// <summary>
        /// 旧版のファイルが使用中で置き換えられないときは、元のファイルに戻して失敗することを検証する
        /// </summary>
        [Fact]
        public void TryApply_FileInUse_RollsBack()
        {
            PortablePackageTestHelper.WriteAppDirectory(
                _target,
                PortablePackageTestHelper.CreateAppFiles("1", ("a.dll", "old a"), ("z.dll", "old z")));
            PortablePackageTestHelper.WriteAppDirectory(
                _source,
                PortablePackageTestHelper.CreateAppFiles("2", ("a.dll", "new a"), ("z.dll", "new z")));

            bool applied;
            string errorMessage;
            using (new FileStream(Path.Combine(_target, "z.dll"), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                applied = PortableUpdateApplier.TryApply(_source, _target, out errorMessage);
            }

            Assert.False(applied);
            Assert.Contains("取り消しました", errorMessage);
            Assert.Equal("exe 1", ReadFile(_target, PortableUpdatePackage.AppExecutableFileName));
            Assert.Equal("old a", ReadFile(_target, "a.dll"));
            Assert.Equal("old z", ReadFile(_target, "z.dll"));
            Assert.Empty(Directory.GetDirectories(_target, PortableUpdateApplier.BackupDirectoryPrefix + "*"));
        }

        /// <summary>
        /// マーカーファイルが無いフォルダー (ポータブル版ではない) は書き換えずに失敗することを検証する
        /// </summary>
        [Fact]
        public void TryApply_TargetWithoutMarker_FailsWithoutChanges()
        {
            WriteFile(_target, PortableUpdatePackage.AppExecutableFileName, "exe 1");
            PortablePackageTestHelper.WriteAppDirectory(_source, PortablePackageTestHelper.CreateAppFiles("2"));

            bool applied = PortableUpdateApplier.TryApply(_source, _target, out string errorMessage);

            Assert.False(applied);
            Assert.Contains("ポータブル版", errorMessage);
            Assert.Equal("exe 1", ReadFile(_target, PortableUpdatePackage.AppExecutableFileName));
        }

        /// <summary>
        /// マニフェストに載る新しい版のファイルが無いときは、書き換えずに失敗することを検証する
        /// </summary>
        [Fact]
        public void TryApply_SourceFileMissing_FailsWithoutChanges()
        {
            PortablePackageTestHelper.WriteAppDirectory(_target, PortablePackageTestHelper.CreateAppFiles("1"));
            PortablePackageTestHelper.WriteAppDirectory(
                _source,
                PortablePackageTestHelper.CreateAppFiles("2", ("missing.dll", "x")));
            File.Delete(Path.Combine(_source, "missing.dll"));

            bool applied = PortableUpdateApplier.TryApply(_source, _target, out string errorMessage);

            Assert.False(applied);
            Assert.Contains("missing.dll", errorMessage);
            Assert.Equal("exe 1", ReadFile(_target, PortableUpdatePackage.AppExecutableFileName));
        }

        /// <summary>
        /// 前回の更新で残った退避フォルダーを削除することを検証する
        /// </summary>
        [Fact]
        public void CleanupLeftoverBackups_RemovesBackupDirectories()
        {
            WriteFile(_target, PortableUpdateApplier.BackupDirectoryPrefix + @"abc\old.dll", "old");
            WriteFile(_target, @"UserData\user.config", "settings");

            PortableUpdateApplier.CleanupLeftoverBackups(_target);

            Assert.Empty(Directory.GetDirectories(_target, PortableUpdateApplier.BackupDirectoryPrefix + "*"));
            Assert.True(File.Exists(Path.Combine(_target, @"UserData\user.config")));
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>ファイルを書き出す</summary>
        private static void WriteFile(string directory, string relativePath, string content)
        {
            string path = Path.Combine(directory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content);
        }

        /// <summary>ファイルを読み込む</summary>
        private static string ReadFile(string directory, string relativePath) =>
            File.ReadAllText(Path.Combine(directory, relativePath));
    }
}
