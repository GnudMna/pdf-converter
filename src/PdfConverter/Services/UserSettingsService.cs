using System;
using PdfConverter.Infrastructure;
using PdfConverter.Properties;
using PdfConverter.Themes;

namespace PdfConverter.Services
{
    /// <summary>
    /// ユーザー設定のリセットとメモリ上の再読み込みを担当する
    /// </summary>
    public sealed class UserSettingsService : IUserSettingsService
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>画像出力設定</summary>
        private readonly IImageExportSettings _imageExportSettings;

        /// <summary>Word → PDF 変換設定</summary>
        private readonly IWordToPdfConversionSettings _wordToPdfSettings;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 依存する設定サービスと起動時初期化結果を受け取る
        /// </summary>
        /// <param name="imageExportSettings">画像出力設定</param>
        /// <param name="wordToPdfSettings">Word → PDF 変換設定</param>
        /// <param name="bootstrapResult">起動時の初期化結果</param>
        public UserSettingsService(
            IImageExportSettings imageExportSettings,
            IWordToPdfConversionSettings wordToPdfSettings,
            UserSettingsBootstrapResult bootstrapResult)
        {
            _imageExportSettings = imageExportSettings ?? throw new ArgumentNullException(nameof(imageExportSettings));
            _wordToPdfSettings = wordToPdfSettings ?? throw new ArgumentNullException(nameof(wordToPdfSettings));
            BootstrapResult = bootstrapResult ?? throw new ArgumentNullException(nameof(bootstrapResult));
        }


        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <inheritdoc/>
        public UserSettingsBootstrapResult BootstrapResult { get; }

        /// <summary>永続化設定を既定値へ戻す (テスト時に差し替え可能)</summary>
        internal Action ResetPersistedSettings { get; set; } = ResetPersistedSettingsCore;

        /// <summary>既定テーマを適用する (テスト時に差し替え可能)</summary>
        internal Action ApplyDefaultTheme { get; set; } = ApplyDefaultThemeCore;


        /********************************************************************************/
        /*                                   イベント                                   */
        /********************************************************************************/
        /// <inheritdoc/>
        public event EventHandler SettingsReset;


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        public void ResetToDefaults()
        {
            ResetPersistedSettings();
            _imageExportSettings.Reload();
            _wordToPdfSettings.Reload();
            ApplyDefaultTheme();

            ApplicationLogger.Info("Settings", "User settings were reset to defaults.");
            SettingsReset?.Invoke(this, EventArgs.Empty);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>ApplicationSettings を既定値へ戻し、移行フラグを下ろす</summary>
        private static void ResetPersistedSettingsCore()
        {
            Settings.Default.Reset();
            Settings.Default.UpgradeRequired = false;
            Settings.Default.Save();
        }

        /// <summary>保存済み (リセット後は既定) テーマを適用する</summary>
        private static void ApplyDefaultThemeCore()
        {
            ThemeManager.Apply(ThemeManager.LoadSavedThemeMode());
        }
    }
}
