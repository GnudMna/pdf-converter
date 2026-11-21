using System;
using System.Configuration;
using System.IO;
using PdfConverter.Properties;

namespace PdfConverter.Infrastructure
{
    /// <summary>
    /// ユーザー設定の破損回復とバージョン移行を起動時に行う
    /// </summary>
    public static class UserSettingsInitializer
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>設定の読み込みを試行する (テスト時に差し替え可能)</summary>
        internal static Action ProbeSettings { get; set; } = ProbeSettingsCore;

        /// <summary>破損した user.config を削除する (テスト時に差し替え可能)</summary>
        internal static Func<ConfigurationErrorsException, bool> TryDeleteCorruptedConfigFile { get; set; } =
            TryDeleteCorruptedConfigFileCore;

        /// <summary>Settings シングルトンを作り直す (テスト時に差し替え可能)</summary>
        internal static Action RecreateSettingsInstance { get; set; } = Settings.RecreateDefaultInstance;

        /// <summary>バージョン移行が必要かどうかを返す (テスト時に差し替え可能)</summary>
        internal static Func<bool> IsUpgradeRequired { get; set; } = () => Settings.Default.UpgradeRequired;

        /// <summary>旧バージョン設定の移行を実行する (テスト時に差し替え可能)</summary>
        internal static Action PerformUpgrade { get; set; } = PerformUpgradeCore;


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 設定の破損回復とバージョン移行を行う
        /// </summary>
        /// <returns>初期化結果</returns>
        public static UserSettingsBootstrapResult Initialize()
        {
            bool recovered = TryRecoverFromCorruption();
            bool upgraded = TryUpgradeIfNeeded();
            return new UserSettingsBootstrapResult(recovered, upgraded);
        }

        /// <summary>
        /// <see cref="ConfigurationErrorsException"/> から user.config のパスを解決する
        /// </summary>
        /// <param name="exception">設定読み込み時の例外</param>
        /// <returns>設定ファイルパス。解決できない場合は <c>null</c></returns>
        internal static string? ResolveConfigFilePath(ConfigurationErrorsException exception)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is ConfigurationErrorsException configException
                    && !string.IsNullOrWhiteSpace(configException.Filename))
                {
                    return configException.Filename;
                }
            }

            try
            {
                return ConfigurationManager
                    .OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal)
                    .FilePath;
            }
            catch (ConfigurationErrorsException)
            {
                return null;
            }
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>破損した設定を検出し、削除してシングルトンを作り直す</summary>
        /// <returns>回復処理を行ったかどうか</returns>
        private static bool TryRecoverFromCorruption()
        {
            try
            {
                ProbeSettings();
                return false;
            }
            catch (ConfigurationErrorsException ex)
            {
                ApplicationLogger.Warning(
                    "Settings",
                    $"ユーザー設定ファイルが破損しているため初期化します: {ResolveConfigFilePath(ex) ?? "(パス不明)"}");
                ApplicationLogger.Error("Settings", "Failed to load user settings", ex);

                TryDeleteCorruptedConfigFile(ex);
                RecreateSettingsInstance();

                try
                {
                    ProbeSettings();
                }
                catch (Exception probeEx)
                {
                    ApplicationLogger.Error("Settings", "Failed to reload user settings after recovery", probeEx);
                }

                return true;
            }
        }

        /// <summary>バージョンアップ後の初回起動で旧設定を移行する</summary>
        /// <returns>移行を実行したかどうか</returns>
        private static bool TryUpgradeIfNeeded()
        {
            try
            {
                if (!IsUpgradeRequired())
                {
                    return false;
                }

                PerformUpgrade();
                ApplicationLogger.Info("Settings", "Migrated user settings from a previous version.");
                return true;
            }
            catch (ConfigurationErrorsException ex)
            {
                ApplicationLogger.Error("Settings", "Failed to upgrade user settings; recovering", ex);
                TryDeleteCorruptedConfigFile(ex);
                RecreateSettingsInstance();
                try
                {
                    Settings.Default.UpgradeRequired = false;
                    Settings.Default.Save();
                }
                catch (Exception saveEx)
                {
                    ApplicationLogger.Error("Settings", "Failed to clear UpgradeRequired after recovery", saveEx);
                }

                return false;
            }
            catch (Exception ex)
            {
                ApplicationLogger.Error("Settings", "Failed to upgrade user settings", ex);
                return false;
            }
        }

        /// <summary>設定プロパティへ触れて読み込みを強制する</summary>
        private static void ProbeSettingsCore()
        {
            _ = Settings.Default.ThemeMode;
            _ = Settings.Default.UpgradeRequired;
        }

        /// <summary>旧バージョン設定を取り込み、移行フラグを下ろす</summary>
        private static void PerformUpgradeCore()
        {
            Settings.Default.Upgrade();
            Settings.Default.UpgradeRequired = false;
            Settings.Default.Save();
        }

        /// <summary>例外情報から特定した user.config を削除する</summary>
        /// <param name="exception">設定読み込み時の例外</param>
        /// <returns>削除に成功したかどうか</returns>
        private static bool TryDeleteCorruptedConfigFileCore(ConfigurationErrorsException exception)
        {
            string? filePath = ResolveConfigFilePath(exception);
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            try
            {
                File.Delete(filePath);
                ApplicationLogger.Info("Settings", $"Deleted corrupted user settings file: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                ApplicationLogger.Error("Settings", $"Failed to delete corrupted user settings file: {filePath}", ex);
                return false;
            }
        }
    }
}
