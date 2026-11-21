using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PdfConverter.Infrastructure;

namespace PdfConverter.Services
{
    /// <summary>
    /// 外部プロセスの終了待機を行うヘルパー
    /// </summary>
    internal static class ProcessWaitHelper
    {
        /// <summary>標準出力 / 標準エラーの読み取り完了待機上限</summary>
        private static readonly TimeSpan StreamReadCompletionTimeout = TimeSpan.FromSeconds(5);
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>終了確認のポーリング間隔 (ms)</summary>
        private const int PollIntervalMs = 100;


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 標準出力 / 標準エラーをリダイレクトしたプロセスの終了を待機し、出力を収集する<br/>
        /// 呼び出し前に <see cref="Process.StartInfo"/> でリダイレクトを有効化し、<see cref="Process.Start"/> を実行しておくこと
        /// </summary>
        /// <param name="process">対象プロセス</param>
        /// <param name="timeout">待機上限時間</param>
        /// <param name="cancellationToken">処理をキャンセルするためのトークン</param>
        /// <param name="timeoutMessage">タイムアウト時に送出する例外メッセージ</param>
        /// <returns>収集した標準出力と標準エラー</returns>
        public static CapturedProcessOutput WaitForExitWithCapturedOutput(
            Process process,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            string timeoutMessage)
        {
            Task<string> stdoutTask = Task.Run(() => ReadStreamToEnd(process.StandardOutput));
            Task<string> stderrTask = Task.Run(() => ReadStreamToEnd(process.StandardError));

            try
            {
                WaitForExit(process, timeout, cancellationToken, timeoutMessage);
            }
            finally
            {
                Task.WaitAll(new[] { stdoutTask, stderrTask }, StreamReadCompletionTimeout);
            }

            return new CapturedProcessOutput(
                GetCompletedTaskResult(stdoutTask),
                GetCompletedTaskResult(stderrTask));
        }

        /// <summary>
        /// プロセスの終了を待機する<br/>
        /// キャンセルまたはタイムアウト時はプロセスを強制終了する
        /// </summary>
        /// <param name="process">対象プロセス</param>
        /// <param name="timeout">待機上限時間</param>
        /// <param name="cancellationToken">処理をキャンセルするためのトークン</param>
        /// <param name="timeoutMessage">タイムアウト時に送出する例外メッセージ</param>
        public static void WaitForExit(
            Process process,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            string timeoutMessage)
        {
            var stopwatch = Stopwatch.StartNew();

            using (cancellationToken.Register(() => TryKillProcess(process)))
            {
                while (!process.HasExited)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (stopwatch.Elapsed >= timeout)
                    {
                        TryKillProcess(process);
                        throw new TimeoutException(timeoutMessage);
                    }

                    process.WaitForExit(PollIntervalMs);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// プロセスを強制終了する
        /// </summary>
        /// <param name="process">対象プロセス</param>
        internal static void TryKillProcess(Process process)
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (Exception ex)
            {
                ApplicationLogger.Warning(nameof(ProcessWaitHelper), "Failed to kill process", ex);
            }
        }

        /// <summary>
        /// ストリームの残りを読み取る
        /// </summary>
        /// <param name="reader">読み取り元</param>
        /// <returns>読み取った文字列</returns>
        private static string ReadStreamToEnd(TextReader reader)
        {
            if (reader == null)
            {
                return string.Empty;
            }

            try
            {
                return reader.ReadToEnd().Trim();
            }
            catch (Exception ex)
            {
                ApplicationLogger.Warning(nameof(ProcessWaitHelper), "Failed to read process stream", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// 完了済みタスクの結果を取得する
        /// </summary>
        /// <param name="task">対象タスク</param>
        /// <returns>タスク結果。未完了または失敗時は空文字列</returns>
        private static string GetCompletedTaskResult(Task<string> task)
        {
            if (task == null || task.Status != TaskStatus.RanToCompletion)
            {
                return string.Empty;
            }

            return task.Result;
        }
    }

    /// <summary>
    /// 外部プロセスから収集した標準出力と標準エラー
    /// </summary>
    internal sealed class CapturedProcessOutput
    {
        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 収集結果を保持する
        /// </summary>
        /// <param name="standardOutput">標準出力</param>
        /// <param name="standardError">標準エラー</param>
        public CapturedProcessOutput(string standardOutput, string standardError)
        {
            StandardOutput = standardOutput ?? string.Empty;
            StandardError = standardError ?? string.Empty;
        }


        /********************************************************************************/
        /*                              パブリックプロパティ                            */
        /********************************************************************************/
        /// <summary>標準出力</summary>
        public string StandardOutput { get; }

        /// <summary>標準エラー</summary>
        public string StandardError { get; }
    }
}
