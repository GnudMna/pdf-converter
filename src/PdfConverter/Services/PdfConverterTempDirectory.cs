using System;
using System.IO;
using PdfConverter.Infrastructure;

namespace PdfConverter.Services
{
    /// <summary>
    /// Word → PDF 変換などで使用する一時ファイル・ディレクトリを管理する
    /// </summary>
    /// <remarks>
    /// 複数インスタンス (GUI・バッチ) の同時実行で互いの一時ファイルを削除しないよう、
    /// 一時ファイルはインスタンス専用のサブディレクトリに作成する<br/>
    /// インスタンスディレクトリ内のロックファイルをプロセス終了まで排他で開き続け、
    /// 他インスタンスの残留ファイル削除から使用中であることを判別できるようにする
    /// </remarks>
    public static class PdfConverterTempDirectory
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>システム一時フォルダー直下のサブフォルダー名</summary>
        public const string FolderName = "PdfConverter";

        /// <summary>インスタンスディレクトリ名の接頭辞</summary>
        public const string InstanceDirectoryPrefix = "instance-";

        /// <summary>インスタンスが使用中であることを示すロックファイル名</summary>
        public const string InstanceLockFileName = ".lock";

        /// <summary>起動時に削除する残留ファイルの既定経過時間</summary>
        public static readonly TimeSpan DefaultStaleAge = TimeSpan.FromHours(24);


        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>インスタンスディレクトリの作成・解放を保護するロックオブジェクト</summary>
        private static readonly object InstanceSync = new object();

        /// <summary>このプロセス固有のインスタンスディレクトリ名</summary>
        private static readonly string InstanceDirectoryName =
            InstanceDirectoryPrefix + Guid.NewGuid().ToString("N");

        /// <summary>使用中を示すために開き続けるロックファイルのストリーム</summary>
        private static FileStream? _instanceLockStream;

        /// <summary>ロックを保持しているインスタンスディレクトリの絶対パス</summary>
        private static string? _lockedInstanceDirectoryPath;

        /// <summary>テスト時にルートパス解決を差し替えるためのプロバイダー</summary>
        internal static Func<string>? RootPathProvider { get; set; }


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
        /// このプロセス専用のインスタンスディレクトリの絶対パスを返す
        /// </summary>
        /// <returns><c>%TEMP%\PdfConverter\instance-xxxx</c> の絶対パス</returns>
        public static string GetInstancePath()
        {
            return Path.Combine(GetRootPath(), InstanceDirectoryName);
        }

        /// <summary>
        /// インスタンスディレクトリを作成し、使用中を示すロックを取得する
        /// </summary>
        /// <returns>インスタンスディレクトリの絶対パス</returns>
        public static string EnsureInstanceDirectoryExists()
        {
            lock (InstanceSync)
            {
                string instancePath = GetInstancePath();
                if (_instanceLockStream != null
                    && string.Equals(_lockedInstanceDirectoryPath, instancePath, StringComparison.OrdinalIgnoreCase))
                {
                    return instancePath;
                }

                ReleaseInstanceLockUnsafe();
                Directory.CreateDirectory(instancePath);

                // 異常終了時も OS がハンドルを閉じた時点でロックファイルが消えるよう DeleteOnClose で開く
                _instanceLockStream = new FileStream(
                    Path.Combine(instancePath, InstanceLockFileName),
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    1,
                    FileOptions.DeleteOnClose);
                _lockedInstanceDirectoryPath = instancePath;
                return instancePath;
            }
        }

        /// <summary>
        /// 一時 PDF ファイルのパスを生成する
        /// </summary>
        /// <returns>インスタンスディレクトリ配下の未使用の一時 PDF パス</returns>
        public static string CreateTempPdfPath()
        {
            string instancePath = EnsureInstanceDirectoryExists();
            return Path.Combine(instancePath, $"{Guid.NewGuid():N}.pdf");
        }

        /// <summary>
        /// LibreOffice 変換などで使用する一時作業ディレクトリを作成する
        /// </summary>
        /// <returns>インスタンスディレクトリ配下に作成した作業ディレクトリの絶対パス</returns>
        public static string CreateTempWorkDirectory()
        {
            string instancePath = EnsureInstanceDirectoryExists();
            string directoryPath = Path.Combine(instancePath, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            return directoryPath;
        }

        /// <summary>
        /// 指定した経過時間より古い一時ファイル・ディレクトリを削除する
        /// </summary>
        /// <param name="maxAge">この時間より古いエントリを削除対象とする</param>
        /// <param name="referenceUtcNow">基準時刻 (テスト用。省略時は <see cref="DateTime.UtcNow"/>)</param>
        /// <remarks>このプロセスおよび実行中の他インスタンスが使用中のディレクトリは削除しない</remarks>
        public static void CleanupStaleEntries(TimeSpan maxAge, DateTime? referenceUtcNow = null)
        {
            string root = GetRootPath();
            if (!Directory.Exists(root))
            {
                return;
            }

            DateTime cutoff = (referenceUtcNow ?? DateTime.UtcNow) - maxAge;
            string ownInstancePath = GetInstancePath();

            foreach (string filePath in Directory.EnumerateFiles(root))
            {
                if (IsOlderThan(filePath, cutoff))
                {
                    TryDeleteFile(filePath);
                }
            }

            foreach (string directoryPath in Directory.EnumerateDirectories(root))
            {
                if (string.Equals(directoryPath, ownInstancePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (IsOlderThan(directoryPath, cutoff) && !IsDirectoryInUse(directoryPath))
                {
                    TryDeleteDirectory(directoryPath);
                }
            }
        }

        /// <summary>
        /// このプロセスのインスタンスディレクトリのロックを解放し、ベストエフォートで削除する
        /// </summary>
        /// <remarks>他インスタンスのディレクトリや共有の更新ファイル置き場には触れない</remarks>
        public static void CleanupInstanceDirectory()
        {
            lock (InstanceSync)
            {
                string instancePath = _lockedInstanceDirectoryPath ?? GetInstancePath();
                ReleaseInstanceLockUnsafe();
                TryDeleteDirectory(instancePath);
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

        /// <summary>
        /// 実行中の他インスタンスがディレクトリのロックファイルを保持しているかどうかを判定する
        /// </summary>
        /// <param name="directoryPath">判定対象ディレクトリ</param>
        /// <returns>ロックファイルを開けない場合は <c>true</c></returns>
        private static bool IsDirectoryInUse(string directoryPath)
        {
            string lockFilePath = Path.Combine(directoryPath, InstanceLockFileName);
            if (!File.Exists(lockFilePath))
            {
                return false;
            }

            try
            {
                using (new FileStream(lockFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    return false;
                }
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (IOException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                // 削除保留中 (所有プロセスの終了処理中) のファイルも使用中として扱う
                return true;
            }
        }

        /// <summary>
        /// 保持中のロックファイルを閉じる (呼び出し元で <see cref="InstanceSync"/> を取得していること)
        /// </summary>
        private static void ReleaseInstanceLockUnsafe()
        {
            if (_instanceLockStream == null)
            {
                return;
            }

            try
            {
                _instanceLockStream.Dispose();
            }
            catch (Exception ex)
            {
                ApplicationLogger.Warning(nameof(PdfConverterTempDirectory), "Failed to release instance lock", ex);
            }

            _instanceLockStream = null;
            _lockedInstanceDirectoryPath = null;
        }
    }
}
