using System;
using System.Configuration;
using System.IO;
using System.Linq;

namespace PdfConverter.Infrastructure
{
    /// <summary>
    /// 会社名を個人名からブランド名へ変える前のフォルダーに残る user.config を、現在のフォルダーへ引き継ぐ
    /// </summary>
    /// <remarks>
    /// .NET は <c>%LOCALAPPDATA%\&lt;会社名&gt;\&lt;実行ファイル名と識別子&gt;\&lt;版&gt;\user.config</c> に設定を保存し、
    /// <c>Upgrade</c> は同じ会社名・同じ識別子の下にある前の版しか探さない<br/>
    /// そのため旧フォルダーの版フォルダーを現在のフォルダーへ複製し、通常のバージョン移行に任せる<br/>
    /// 複製に成功したら、個人名のフォルダーが残らないよう旧フォルダーを削除する
    /// </remarks>
    public static class LegacySettingsMigrator
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>旧会社名のフォルダー名 (空白は .NET により _ に置換される)</summary>
        internal const string LegacyCompanyDirectoryName = "Yuto_Hirata";

        /// <summary>複製中の一時フォルダーの接尾辞</summary>
        private const string MigratingSuffix = ".migrating";


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 現在の設定フォルダーに版フォルダーがなく、旧フォルダーに設定があれば引き継いで旧フォルダーを削除する
        /// </summary>
        /// <returns>旧フォルダーから複製したかどうか</returns>
        /// <remarks>ポータブル版は旧フォルダーを使ったことがないため、何もしない</remarks>
        public static bool TryMigrate()
        {
            if (PortableMode.IsEnabled)
            {
                return false;
            }

            string? currentConfigFilePath = ResolveCurrentConfigFilePath();
            return currentConfigFilePath != null
                && TryMigrate(currentConfigFilePath, LegacyCompanyDirectoryName);
        }

        /// <summary>
        /// 現在の user.config の保存先を基準に、旧会社名のフォルダーから設定を引き継いで旧フォルダーを削除する
        /// </summary>
        /// <param name="currentConfigFilePath">現在の版の user.config のパス</param>
        /// <param name="legacyCompanyDirectoryName">旧会社名のフォルダー名</param>
        /// <returns>旧フォルダーから複製したかどうか (旧フォルダーの削除に失敗しても <c>true</c>)</returns>
        internal static bool TryMigrate(string currentConfigFilePath, string legacyCompanyDirectoryName)
        {
            string? temporaryDirectory = null;
            try
            {
                string? appDirectory = Path.GetDirectoryName(Path.GetDirectoryName(currentConfigFilePath));
                string? companyDirectory = Path.GetDirectoryName(appDirectory);
                string? rootDirectory = Path.GetDirectoryName(companyDirectory);
                if (string.IsNullOrEmpty(appDirectory)
                    || string.IsNullOrEmpty(companyDirectory)
                    || string.IsNullOrEmpty(rootDirectory)
                    || string.Equals(
                        Path.GetFileName(companyDirectory),
                        legacyCompanyDirectoryName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                string legacyAppDirectory = Path.Combine(
                    rootDirectory,
                    legacyCompanyDirectoryName,
                    Path.GetFileName(appDirectory));
                if (!Directory.Exists(legacyAppDirectory)
                    || (Directory.Exists(appDirectory) && Directory.EnumerateDirectories(appDirectory).Any()))
                {
                    return false;
                }

                temporaryDirectory = appDirectory + MigratingSuffix;
                if (Directory.Exists(temporaryDirectory))
                {
                    Directory.Delete(temporaryDirectory, recursive: true);
                }

                CopyDirectory(legacyAppDirectory, temporaryDirectory);
                if (Directory.Exists(appDirectory))
                {
                    Directory.Delete(appDirectory, recursive: false);
                }

                Directory.Move(temporaryDirectory, appDirectory);
                ApplicationLogger.Info("Settings", $"Copied user settings from legacy folder: {legacyAppDirectory}");
                DeleteLegacyDirectory(legacyAppDirectory);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ApplicationLogger.Error("Settings", "Failed to copy user settings from legacy folder", ex);
                TryDeleteDirectory(temporaryDirectory);
                return false;
            }
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>現在の版の user.config のパスを解決する</summary>
        /// <returns>パス。解決できない場合は <c>null</c></returns>
        private static string? ResolveCurrentConfigFilePath()
        {
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

        /// <summary>フォルダーを配下のファイルごと複製する</summary>
        /// <param name="sourceDirectory">複製元</param>
        /// <param name="destinationDirectory">複製先</param>
        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);
            foreach (string file in Directory.EnumerateFiles(sourceDirectory))
            {
                File.Copy(file, Path.Combine(destinationDirectory, Path.GetFileName(file)));
            }

            foreach (string directory in Directory.EnumerateDirectories(sourceDirectory))
            {
                CopyDirectory(directory, Path.Combine(destinationDirectory, Path.GetFileName(directory)));
            }
        }

        /// <summary>複製済みの旧フォルダーを削除し、空になった旧会社名のフォルダーも片付ける</summary>
        /// <param name="legacyAppDirectory">旧会社名フォルダー配下のアプリのフォルダー</param>
        /// <remarks>複製は完了しているため、削除に失敗しても警告を残して続行する</remarks>
        private static void DeleteLegacyDirectory(string legacyAppDirectory)
        {
            try
            {
                Directory.Delete(legacyAppDirectory, recursive: true);

                string? legacyCompanyDirectory = Path.GetDirectoryName(legacyAppDirectory);
                if (legacyCompanyDirectory != null && !Directory.EnumerateFileSystemEntries(legacyCompanyDirectory).Any())
                {
                    Directory.Delete(legacyCompanyDirectory, recursive: false);
                }

                ApplicationLogger.Info("Settings", $"Deleted legacy user settings folder: {legacyAppDirectory}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ApplicationLogger.Warning("Settings", $"Failed to delete legacy user settings folder: {legacyAppDirectory}", ex);
            }
        }

        /// <summary>途中まで複製した一時フォルダーを後始末する</summary>
        /// <param name="directory">削除するフォルダー</param>
        private static void TryDeleteDirectory(string? directory)
        {
            try
            {
                if (directory != null && Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ApplicationLogger.Warning("Settings", $"Failed to delete temporary folder: {directory}", ex);
            }
        }
    }
}
