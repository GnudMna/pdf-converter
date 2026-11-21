using System;
using System.IO;
using PdfConverter.Infrastructure;

namespace PdfConverter.Services
{
    /// <summary>
    /// Word → PDF 変換などで使用する一時ファイル・ディレクトリを管理する
    /// </summary>
    public static class PdfConverterTempDirectory
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>システム一時フォルダー直下のサブフォルダー名</summary>
        public const string FolderName = "PdfConverter";

        /// <summary>起動時に削除する残留ファイルの既定経過時間</summary>
        public static readonly TimeSpan DefaultStaleAge = TimeSpan.FromHours(24);


        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>テスト時にルートパス解決を差し替えるためのプロバイダー</summary>
        internal static Func<string> RootPathProvider { get; set; }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 一時ファイル用ルートディレクトリの絶対パスを返す
        /// </summary>
        /// <returns><c>%TEMP%\PdfConverter</c> の絶対パス</returns>
        public static string GetRootPath()
        {
            if (RootPathProvider != null)
            {
                return RootPathProvider();
            }

            return Path.Combine(Path.GetTempPath(), FolderName);
        }

        /// <summary>
        /// 一時ファイル用ルートディレクトリを作成する
        /// </summary>
        public static void EnsureRootExists()
        {
            Directory.CreateDirectory(GetRootPath());
        }

        /// <summary>
        /// 一時 PDF ファイルのパスを生成する
        /// </summary>
        /// <returns>未使用の一時 PDF パス</returns>
        public static string CreateTempPdfPath()
        {
            EnsureRootExists();
            return Path.Combine(GetRootPath(), $"{Guid.NewGuid():N}.pdf");
        }

        /// <summary>
        /// LibreOffice 変換などで使用する一時作業ディレクトリを作成する
        /// </summary>
        /// <returns>作成した作業ディレクトリの絶対パス</returns>
        public static string CreateTempWorkDirectory()
        {
            string directoryPath = Path.Combine(GetRootPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            return directoryPath;
        }

        /// <summary>
        /// 指定した経過時間より古い一時ファイル・ディレクトリを削除する
        /// </summary>
        /// <param name="maxAge">この時間より古いエントリを削除対象とする</param>
        /// <param name="referenceUtcNow">基準時刻 (テスト用。省略時は <see cref="DateTime.UtcNow"/>)</param>
        public static void CleanupStaleEntries(TimeSpan maxAge, DateTime? referenceUtcNow = null)
        {
            string root = GetRootPath();
            if (!Directory.Exists(root))
            {
                return;
            }

            DateTime cutoff = (referenceUtcNow ?? DateTime.UtcNow) - maxAge;

            foreach (string filePath in Directory.EnumerateFiles(root))
            {
                if (IsOlderThan(filePath, cutoff))
                {
                    TryDeleteFile(filePath);
                }
            }

            foreach (string directoryPath in Directory.EnumerateDirectories(root))
            {
                if (IsOlderThan(directoryPath, cutoff))
                {
                    TryDeleteDirectory(directoryPath);
                }
            }
        }

        /// <summary>
        /// 一時ルート配下のファイル・ディレクトリをベストエフォートで削除する
        /// </summary>
        public static void TryCleanupAll()
        {
            string root = GetRootPath();
            if (!Directory.Exists(root))
            {
                return;
            }

            foreach (string filePath in Directory.EnumerateFiles(root))
            {
                TryDeleteFile(filePath);
            }

            foreach (string directoryPath in Directory.EnumerateDirectories(root))
            {
                TryDeleteDirectory(directoryPath);
            }
        }

        /// <summary>
        /// 一時ファイルを削除する
        /// </summary>
        /// <param name="path">削除対象のファイルパス</param>
        public static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                ApplicationLogger.Warning(nameof(PdfConverterTempDirectory), $"Failed to delete file: {path}", ex);
            }
        }

        /// <summary>
        /// 一時ディレクトリを再帰的に削除する
        /// </summary>
        /// <param name="directoryPath">削除対象ディレクトリ</param>
        public static void TryDeleteDirectory(string directoryPath)
        {
            try
            {
                if (Directory.Exists(directoryPath))
                {
                    Directory.Delete(directoryPath, recursive: true);
                }
            }
            catch (Exception ex)
            {
                ApplicationLogger.Warning(nameof(PdfConverterTempDirectory), $"Failed to delete directory: {directoryPath}", ex);
            }
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// 指定したパスの最終更新時刻が基準より古いかどうかを判定する
        /// </summary>
        /// <param name="path">ファイルまたはディレクトリのパス</param>
        /// <param name="cutoffUtc">基準時刻 (UTC)</param>
        /// <returns>最終更新が基準より古い場合は <c>true</c></returns>
        private static bool IsOlderThan(string path, DateTime cutoffUtc)
        {
            DateTime lastWriteUtc = File.Exists(path)
                ? File.GetLastWriteTimeUtc(path)
                : Directory.GetLastWriteTimeUtc(path);

            return lastWriteUtc < cutoffUtc;
        }
    }
}
