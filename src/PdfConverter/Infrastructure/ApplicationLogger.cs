using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace PdfConverter.Infrastructure
{
    /// <summary>
    /// アプリケーションの操作ログとエラーログをファイルへ記録する
    /// </summary>
    public static class ApplicationLogger
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>ログファイルの最大サイズ (バイト)。テスト時に上書き可能</summary>
        internal static int? MaxLogFileBytesForTesting { get; set; }

        /// <summary>ローテーション後に保持するアーカイブ数</summary>
        private const int MaxArchivedFiles = 5;

        /// <summary>既定のログファイル最大サイズ (バイト)</summary>
        private const int DefaultMaxLogFileBytes = 2 * 1024 * 1024;

        /// <summary>他プロセスのログ書き込みを待つ最大時間</summary>
        private static readonly TimeSpan MutexWaitTimeout = TimeSpan.FromSeconds(5);


        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>ログ書き込みの排他制御</summary>
        private static readonly object SyncRoot = new object();


        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>ログ出力先ディレクトリ (テスト時に上書き可能)</summary>
        public static string? LogDirectory { get; set; }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>操作ログを記録する</summary>
        /// <param name="source">発生元の識別子</param>
        /// <param name="message">ログメッセージ</param>
        public static void Info(string source, string message)
        {
            Write(LogLevel.Info, source, message, null);
        }

        /// <summary>警告ログを記録する</summary>
        /// <param name="source">発生元の識別子</param>
        /// <param name="message">ログメッセージ</param>
        public static void Warning(string source, string message)
        {
            Write(LogLevel.Warning, source, message, null);
        }

        /// <summary>警告ログと例外詳細を記録する</summary>
        /// <param name="source">発生元の識別子</param>
        /// <param name="message">ログメッセージ</param>
        /// <param name="exception">関連する例外</param>
        public static void Warning(string source, string message, Exception exception)
        {
            Write(LogLevel.Warning, source, message, exception);
        }

        /// <summary>エラーログを記録する</summary>
        /// <param name="source">発生元の識別子</param>
        /// <param name="message">ログメッセージ</param>
        /// <param name="exception">関連する例外</param>
        public static void Error(string source, string message, Exception? exception = null)
        {
            Write(LogLevel.Error, source, message, exception);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>ログレベルに応じてファイルへ追記する</summary>
        /// <param name="level">ログレベル</param>
        /// <param name="source">発生元の識別子</param>
        /// <param name="message">ログメッセージ</param>
        /// <param name="exception">関連する例外</param>
        private static void Write(LogLevel level, string source, string message, Exception? exception)
        {
            try
            {
                string directory = LogDirectory ?? GetDefaultLogDirectory();
                Directory.CreateDirectory(directory);

                string fileName = level == LogLevel.Error ? "error.log" : "app.log";
                string logPath = Path.Combine(directory, fileName);
                string entry = BuildEntry(level, source, message, exception);

                lock (SyncRoot)
                {
                    // 画面とバッチ変換が同時に動いても、他方のログ行を失ったりローテーションが競合したりしないようプロセス間でも排他する
                    using Mutex mutex = new Mutex(false, GetMutexName(directory));
                    bool acquired = AcquireMutex(mutex);
                    try
                    {
                        RotateIfNeeded(logPath);
                        File.AppendAllText(logPath, entry, Encoding.UTF8);
                    }
                    finally
                    {
                        if (acquired)
                        {
                            mutex.ReleaseMutex();
                        }
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>プロセス間の排他用ミューテックスを取得する</summary>
        /// <param name="mutex">対象のミューテックス</param>
        /// <returns>取得できた場合は true (待機がタイムアウトした場合は false。ログを落とさないよう排他なしで書き込みを続ける)</returns>
        private static bool AcquireMutex(Mutex mutex)
        {
            try
            {
                return mutex.WaitOne(MutexWaitTimeout);
            }
            catch (AbandonedMutexException)
            {
                // 他プロセスがロックを持ったまま終了した場合。所有権は引き継げている
                return true;
            }
        }

        /// <summary>ログ出力先ディレクトリごとのミューテックス名を返す</summary>
        /// <param name="directory">ログ出力先ディレクトリ</param>
        /// <returns>ミューテックス名</returns>
        internal static string GetMutexName(string directory)
        {
            string normalized = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
            return $"Local\\PdfConverter.Log.{Convert.ToHexString(hash, 0, 8)}";
        }

        /// <summary>ログエントリの文字列を組み立てる</summary>
        /// <param name="level">ログレベル</param>
        /// <param name="source">発生元の識別子</param>
        /// <param name="message">ログメッセージ</param>
        /// <param name="exception">関連する例外</param>
        /// <returns>追記するログ行</returns>
        private static string BuildEntry(LogLevel level, string source, string message, Exception? exception)
        {
            var builder = new StringBuilder();
            builder.Append('[');
            builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            builder.Append("] [");
            builder.Append(level);
            builder.Append("] [");
            builder.Append(source ?? string.Empty);
            builder.Append("] ");
            builder.Append(message ?? string.Empty);

            if (exception != null)
            {
                builder.AppendLine();
                builder.Append(exception);
            }

            builder.AppendLine();
            builder.AppendLine();
            return builder.ToString();
        }

        /// <summary>ログファイルが上限を超えた場合にローテーションする</summary>
        /// <param name="logPath">対象ログファイルのパス</param>
        private static void RotateIfNeeded(string logPath)
        {
            if (!File.Exists(logPath))
            {
                return;
            }

            int maxBytes = GetMaxLogFileBytes();
            if (new FileInfo(logPath).Length < maxBytes)
            {
                return;
            }

            string oldestPath = $"{logPath}.{MaxArchivedFiles}";
            if (File.Exists(oldestPath))
            {
                File.Delete(oldestPath);
            }

            for (int index = MaxArchivedFiles - 1; index >= 1; index--)
            {
                string sourcePath = $"{logPath}.{index}";
                if (!File.Exists(sourcePath))
                {
                    continue;
                }

                string destinationPath = $"{logPath}.{index + 1}";
                File.Move(sourcePath, destinationPath);
            }

            string firstArchivePath = $"{logPath}.1";
            File.Move(logPath, firstArchivePath);
        }

        /// <summary>ログファイルの最大サイズを返す</summary>
        /// <returns>最大サイズ (バイト)</returns>
        private static int GetMaxLogFileBytes()
        {
            return MaxLogFileBytesForTesting ?? DefaultMaxLogFileBytes;
        }

        /// <summary>既定のログ出力先ディレクトリを返す</summary>
        /// <returns>ログディレクトリの絶対パス (ポータブル版はアプリフォルダー内)</returns>
        private static string GetDefaultLogDirectory()
        {
            if (PortableMode.IsEnabled)
            {
                return PortableMode.LogDirectory;
            }

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appData, "PDF Converter", "logs");
        }


        /********************************************************************************/
        /*                                   ネスト型                                   */
        /********************************************************************************/
        /// <summary>ログの重要度</summary>
        private enum LogLevel
        {
            /// <summary>通常の操作ログ</summary>
            Info,

            /// <summary>警告</summary>
            Warning,

            /// <summary>エラー</summary>
            Error,
        }
    }
}
