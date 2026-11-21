using System;
using Moq;
using PdfConverter.Infrastructure;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="UserSettingsService"/> のリセット処理を検証する
    /// </summary>
    public class UserSettingsServiceTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// リセット時に永続化設定・各設定サービス・テーマ適用・イベント通知が行われることを検証する
        /// </summary>
        [Fact]
        public void ResetToDefaults_ReloadsSettingsAndRaisesEvent()
        {
            var imageExport = new Mock<IImageExportSettings>();
            var wordSettings = new Mock<IWordToPdfConversionSettings>();
            var service = new UserSettingsService(
                imageExport.Object,
                wordSettings.Object,
                new UserSettingsBootstrapResult(recoveredFromCorruption: false, upgraded: false));

            int resetPersistedCount = 0;
            int applyThemeCount = 0;
            int resetEventCount = 0;
            service.ResetPersistedSettings = () => resetPersistedCount++;
            service.ApplyDefaultTheme = () => applyThemeCount++;
            service.SettingsReset += (_, __) => resetEventCount++;

            service.ResetToDefaults();

            Assert.Equal(1, resetPersistedCount);
            imageExport.Verify(s => s.Reload(), Times.Once);
            wordSettings.Verify(s => s.Reload(), Times.Once);
            Assert.Equal(1, applyThemeCount);
            Assert.Equal(1, resetEventCount);
        }

        /// <summary>
        /// 起動時初期化結果が保持されることを検証する
        /// </summary>
        [Fact]
        public void BootstrapResult_IsPreserved()
        {
            var bootstrap = new UserSettingsBootstrapResult(recoveredFromCorruption: true, upgraded: true);
            var service = new UserSettingsService(
                Mock.Of<IImageExportSettings>(),
                Mock.Of<IWordToPdfConversionSettings>(),
                bootstrap);

            Assert.Same(bootstrap, service.BootstrapResult);
            Assert.True(service.BootstrapResult.RecoveredFromCorruption);
            Assert.True(service.BootstrapResult.Upgraded);
        }
    }
}
