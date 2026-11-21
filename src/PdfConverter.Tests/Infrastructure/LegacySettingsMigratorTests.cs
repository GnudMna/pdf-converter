using System;
using System.IO;
using PdfConverter.Infrastructure;
using PdfConverter.Tests.Helpers;
using Xunit;

namespace PdfConverter.Tests.Infrastructure
{
    /// <summary>
    /// <see cref="LegacySettingsMigrator"/> の旧フォルダーからの設定引き継ぎを検証する
    /// </summary>
    [Collection(GlobalStateTestCollection.Name)]
    public sealed class LegacySettingsMigratorTests : IDisposable
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/

        private const string LegacyCompany = "Legacy_Company";
        private const string CurrentCompany = "CurrentCompany";
        private const string AppDirectoryName = "PdfConverter.exe_Url_test";


        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/

        private readonly string _root = Path.Combine(Path.GetTempPath(), $"pdf-converter-settings-{Guid.NewGuid():N}");


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/

        public LegacySettingsMigratorTests()
        {
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            PdfTestAssetFactory.DeleteDirectoryIfExists(_root);
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/

        /// <summary>
        /// 旧フォルダーの全バージョンの設定を現在のフォルダーへ複製し、旧フォルダーを削除することを検証する
        /// </summary>
        [Fact]
        public void TryMigrate_LegacySettingsExist_CopiesAllVersions()
        {
            WriteLegacyConfig("0.6.1.0", "old-1");
            WriteLegacyConfig("0.6.2.0", "old-2");

            bool migrated = LegacySettingsMigrator.TryMigrate(CurrentConfigFilePath("0.7.0.0"), LegacyCompany);

            Assert.True(migrated);
            Assert.Equal("old-1", File.ReadAllText(CurrentConfigFilePath("0.6.1.0")));
            Assert.Equal("old-2", File.ReadAllText(CurrentConfigFilePath("0.6.2.0")));
            Assert.False(Directory.Exists(Path.Combine(_root, LegacyCompany)));
            Assert.False(Directory.Exists(CurrentAppDirectory() + ".migrating"));
        }

        /// <summary>
        /// 旧会社名のフォルダーに他のアプリのフォルダーがあれば残すことを検証する
        /// </summary>
        [Fact]
        public void TryMigrate_LegacyCompanyHasOtherApp_KeepsOtherAppFolder()
        {
            WriteLegacyConfig("0.6.2.0", "old");
            string otherApp = Path.Combine(_root, LegacyCompany, "Other.exe_Url_test");
            Directory.CreateDirectory(otherApp);

            bool migrated = LegacySettingsMigrator.TryMigrate(CurrentConfigFilePath("0.7.0.0"), LegacyCompany);

            Assert.True(migrated);
            Assert.True(Directory.Exists(otherApp));
            Assert.False(Directory.Exists(Path.Combine(_root, LegacyCompany, AppDirectoryName)));
        }

        /// <summary>
        /// 現在のフォルダーに設定が既にあるときは旧フォルダーも残すことを検証する
        /// </summary>
        [Fact]
        public void TryMigrate_CurrentSettingsExist_KeepsLegacyFolder()
        {
            WriteLegacyConfig("0.6.2.0", "old");
            Directory.CreateDirectory(Path.GetDirectoryName(CurrentConfigFilePath("0.6.3.0"))!);

            LegacySettingsMigrator.TryMigrate(CurrentConfigFilePath("0.7.0.0"), LegacyCompany);

            Assert.True(File.Exists(LegacyConfigFilePath("0.6.2.0")));
        }

        /// <summary>
        /// 旧フォルダーが無いときは何もしないことを検証する
        /// </summary>
        [Fact]
        public void TryMigrate_NoLegacyFolder_ReturnsFalse()
        {
            bool migrated = LegacySettingsMigrator.TryMigrate(CurrentConfigFilePath("0.7.0.0"), LegacyCompany);

            Assert.False(migrated);
            Assert.False(Directory.Exists(CurrentAppDirectory()));
        }

        /// <summary>
        /// 現在のフォルダーに設定が既にあるときは上書きしないことを検証する
        /// </summary>
        [Fact]
        public void TryMigrate_CurrentSettingsExist_KeepsCurrentSettings()
        {
            WriteLegacyConfig("0.6.2.0", "old");
            Directory.CreateDirectory(Path.GetDirectoryName(CurrentConfigFilePath("0.6.3.0"))!);
            File.WriteAllText(CurrentConfigFilePath("0.6.3.0"), "new");

            bool migrated = LegacySettingsMigrator.TryMigrate(CurrentConfigFilePath("0.7.0.0"), LegacyCompany);

            Assert.False(migrated);
            Assert.Equal("new", File.ReadAllText(CurrentConfigFilePath("0.6.3.0")));
            Assert.False(File.Exists(CurrentConfigFilePath("0.6.2.0")));
        }

        /// <summary>
        /// 現在の会社名が旧会社名と同じときは何もしないことを検証する
        /// </summary>
        [Fact]
        public void TryMigrate_SameCompanyName_ReturnsFalse()
        {
            WriteLegacyConfig("0.6.2.0", "old");
            string legacyConfigFilePath = LegacyConfigFilePath("0.7.0.0");

            bool migrated = LegacySettingsMigrator.TryMigrate(legacyConfigFilePath, LegacyCompany);

            Assert.False(migrated);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/

        private string CurrentAppDirectory() => Path.Combine(_root, CurrentCompany, AppDirectoryName);

        private string CurrentConfigFilePath(string version) =>
            Path.Combine(CurrentAppDirectory(), version, "user.config");

        private string LegacyConfigFilePath(string version) =>
            Path.Combine(_root, LegacyCompany, AppDirectoryName, version, "user.config");

        private void WriteLegacyConfig(string version, string content)
        {
            string path = LegacyConfigFilePath(version);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
    }
}
