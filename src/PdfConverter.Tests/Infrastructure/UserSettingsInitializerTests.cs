using System;
using System.Configuration;
using PdfConverter.Infrastructure;
using Xunit;

namespace PdfConverter.Tests.Infrastructure
{
    /// <summary>
    /// <see cref="UserSettingsInitializer"/> の破損回復とバージョン移行を検証する
    /// </summary>
    public sealed class UserSettingsInitializerTests : IDisposable
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        private readonly Action _originalProbeSettings;
        private readonly Func<ConfigurationErrorsException, bool> _originalTryDelete;
        private readonly Action _originalRecreate;
        private readonly Func<bool> _originalIsUpgradeRequired;
        private readonly Action _originalPerformUpgrade;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        public UserSettingsInitializerTests()
        {
            _originalProbeSettings = UserSettingsInitializer.ProbeSettings;
            _originalTryDelete = UserSettingsInitializer.TryDeleteCorruptedConfigFile;
            _originalRecreate = UserSettingsInitializer.RecreateSettingsInstance;
            _originalIsUpgradeRequired = UserSettingsInitializer.IsUpgradeRequired;
            _originalPerformUpgrade = UserSettingsInitializer.PerformUpgrade;
        }

        public void Dispose()
        {
            UserSettingsInitializer.ProbeSettings = _originalProbeSettings;
            UserSettingsInitializer.TryDeleteCorruptedConfigFile = _originalTryDelete;
            UserSettingsInitializer.RecreateSettingsInstance = _originalRecreate;
            UserSettingsInitializer.IsUpgradeRequired = _originalIsUpgradeRequired;
            UserSettingsInitializer.PerformUpgrade = _originalPerformUpgrade;
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 正常な設定では回復も移行も行わないことを検証する
        /// </summary>
        [Fact]
        public void Initialize_HealthySettings_ReturnsNoRecoveryAndNoUpgrade()
        {
            int probeCount = 0;
            int recreateCount = 0;
            int upgradeCount = 0;
            UserSettingsInitializer.ProbeSettings = () => probeCount++;
            UserSettingsInitializer.IsUpgradeRequired = () => false;
            UserSettingsInitializer.RecreateSettingsInstance = () => recreateCount++;
            UserSettingsInitializer.PerformUpgrade = () => upgradeCount++;

            UserSettingsBootstrapResult result = UserSettingsInitializer.Initialize();

            Assert.False(result.RecoveredFromCorruption);
            Assert.False(result.Upgraded);
            Assert.Equal(1, probeCount);
            Assert.Equal(0, recreateCount);
            Assert.Equal(0, upgradeCount);
        }

        /// <summary>
        /// 破損検出時に設定ファイル削除とシングルトン再作成を行うことを検証する
        /// </summary>
        [Fact]
        public void Initialize_CorruptedSettings_RecoversAndContinues()
        {
            int probeCount = 0;
            int deleteCount = 0;
            int recreateCount = 0;
            UserSettingsInitializer.ProbeSettings = () =>
            {
                probeCount++;
                if (probeCount == 1)
                {
                    throw new ConfigurationErrorsException("broken", "user.config", 1);
                }
            };
            UserSettingsInitializer.TryDeleteCorruptedConfigFile = _ =>
            {
                deleteCount++;
                return true;
            };
            UserSettingsInitializer.RecreateSettingsInstance = () => recreateCount++;
            UserSettingsInitializer.IsUpgradeRequired = () => false;

            UserSettingsBootstrapResult result = UserSettingsInitializer.Initialize();

            Assert.True(result.RecoveredFromCorruption);
            Assert.False(result.Upgraded);
            Assert.Equal(2, probeCount);
            Assert.Equal(1, deleteCount);
            Assert.Equal(1, recreateCount);
        }

        /// <summary>
        /// UpgradeRequired が true のとき移行を実行することを検証する
        /// </summary>
        [Fact]
        public void Initialize_UpgradeRequired_PerformsUpgrade()
        {
            int upgradeCount = 0;
            UserSettingsInitializer.ProbeSettings = () => { };
            UserSettingsInitializer.IsUpgradeRequired = () => true;
            UserSettingsInitializer.PerformUpgrade = () => upgradeCount++;

            UserSettingsBootstrapResult result = UserSettingsInitializer.Initialize();

            Assert.False(result.RecoveredFromCorruption);
            Assert.True(result.Upgraded);
            Assert.Equal(1, upgradeCount);
        }

        /// <summary>
        /// 例外から user.config パスを解決できることを検証する
        /// </summary>
        [Fact]
        public void ResolveConfigFilePath_UsesFilenameFromException()
        {
            var exception = new ConfigurationErrorsException("broken", @"C:\temp\user.config", 3);

            Assert.Equal(@"C:\temp\user.config", UserSettingsInitializer.ResolveConfigFilePath(exception));
        }
    }
}
