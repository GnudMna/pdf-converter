using System;
using System.Configuration;
using System.IO;
using PdfConverter.Infrastructure;
using PdfConverter.Tests.Helpers;
using Xunit;

namespace PdfConverter.Tests.Infrastructure
{
    /// <summary>
    /// <see cref="PortableSettingsProvider"/> の保存・読み込み・リセット・破損時の報告を検証する
    /// </summary>
    [Collection(GlobalStateTestCollection.Name)]
    public sealed class PortableSettingsProviderTests : IDisposable
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>テスト用のルート</summary>
        private readonly string _root;

        /// <summary>設定ファイルのパス</summary>
        private readonly string _filePath;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// テスト用のフォルダーを用意し、ログの出力先を切り替える
        /// </summary>
        public PortableSettingsProviderTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "PdfConverter-tests", Guid.NewGuid().ToString("N"));
            _filePath = Path.Combine(_root, "UserData", "user.config");
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
        /// 設定ファイルが無いときは既定値を返すことを検証する
        /// </summary>
        [Fact]
        public void Load_FileMissing_ReturnsDefaults()
        {
            var settings = new TestSettings(_filePath);

            Assert.Equal("System", settings.ThemeMode);
            Assert.True(settings.PreserveTransparency);
            Assert.Equal(90, settings.JpegQuality);
        }

        /// <summary>
        /// 保存した値を、別のインスタンスで読み込めることを検証する
        /// </summary>
        [Fact]
        public void Save_ThenLoad_RoundTripsValues()
        {
            var settings = new TestSettings(_filePath)
            {
                ThemeMode = "Dark",
                PreserveTransparency = false,
                JpegQuality = 75,
            };
            settings.Save();

            var reloaded = new TestSettings(_filePath);

            Assert.True(File.Exists(_filePath));
            Assert.Equal("Dark", reloaded.ThemeMode);
            Assert.False(reloaded.PreserveTransparency);
            Assert.Equal(75, reloaded.JpegQuality);
        }

        /// <summary>
        /// リセットすると保存済みの値を消し、既定値に戻ることを検証する
        /// </summary>
        [Fact]
        public void Reset_AfterSave_RestoresDefaults()
        {
            var settings = new TestSettings(_filePath)
            {
                ThemeMode = "Dark",
            };
            settings.Save();

            settings.Reset();

            Assert.Equal("System", settings.ThemeMode);
            Assert.Equal("System", new TestSettings(_filePath).ThemeMode);
        }

        /// <summary>
        /// 前の版からの引き継ぎを実行しても、保存済みの値が変わらないことを検証する
        /// </summary>
        [Fact]
        public void Upgrade_KeepsCurrentValues()
        {
            var settings = new TestSettings(_filePath)
            {
                ThemeMode = "Dark",
            };
            settings.Save();

            var reloaded = new TestSettings(_filePath);
            reloaded.Upgrade();

            Assert.Equal("Dark", reloaded.ThemeMode);
        }

        /// <summary>
        /// 壊れた設定ファイルは、ファイル名付きの <see cref="ConfigurationErrorsException"/> として報告することを検証する
        /// </summary>
        [Fact]
        public void Load_CorruptedFile_ThrowsWithFileName()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath));
            File.WriteAllText(_filePath, "<settings><setting name=");
            var settings = new TestSettings(_filePath);

            ConfigurationErrorsException ex = Assert.Throws<ConfigurationErrorsException>(() => settings.ThemeMode);

            Assert.Equal(_filePath, ex.Filename);
        }


        /********************************************************************************/
        /*                                   ネスト型                                   */
        /********************************************************************************/
        /// <summary>
        /// アプリの <c>Settings</c> と同じ方法で保存先を差し替えたテスト用の設定
        /// </summary>
        private sealed class TestSettings : ApplicationSettingsBase
        {
            public TestSettings(string filePath)
            {
                var provider = new PortableSettingsProvider(filePath);
                Providers.Add(provider);
                foreach (SettingsProperty property in Properties)
                {
                    property.Provider = provider;
                }
            }

            [UserScopedSetting]
            [DefaultSettingValue("System")]
            public string ThemeMode
            {
                get => (string)this[nameof(ThemeMode)];
                set => this[nameof(ThemeMode)] = value;
            }

            [UserScopedSetting]
            [DefaultSettingValue("True")]
            public bool PreserveTransparency
            {
                get => (bool)this[nameof(PreserveTransparency)];
                set => this[nameof(PreserveTransparency)] = value;
            }

            [UserScopedSetting]
            [DefaultSettingValue("90")]
            public int JpegQuality
            {
                get => (int)this[nameof(JpegQuality)];
                set => this[nameof(JpegQuality)] = value;
            }
        }
    }
}
