using System;
using System.Diagnostics;
using System.Threading;

namespace PdfConverter.Services
{
    /// <summary>
    /// 外部プロセスの終了待機を行うヘルパー
    /// </summary>
    internal static class ProcessWaitHelper
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>終了確認のポーリング間隔 (ms)</summary>
        private const int PollIntervalMs = 100;


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
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
            catch
            {
            }
        }
    }
}
