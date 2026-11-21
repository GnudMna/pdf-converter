using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PdfConverter.Infrastructure
{
    /// <summary>
    /// バッチモード向けのコンソール出力補助
    /// </summary>
    internal static class BatchConsoleHelper
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>親プロセスのコンソールへ接続するための識別子</summary>
        private const int AttachParentProcess = -1;


        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>コンソールを確保済みかどうか</summary>
        private static volatile bool _consoleReady;

        /// <summary>コンソール確保処理の排他用 (並列保存の進捗通知から同時に呼ばれうるため)</summary>
        private static readonly object ConsoleLock = new object();


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>標準出力へ 1 行書き込む</summary>
        /// <param name="message">メッセージ</param>
        public static void WriteLine(string? message)
        {
            EnsureConsole();
            Console.Out.WriteLine(message ?? string.Empty);
        }

        /// <summary>標準エラーへ 1 行書き込む</summary>
        /// <param name="message">メッセージ</param>
        public static void WriteErrorLine(string? message)
        {
            EnsureConsole();
            Console.Error.WriteLine(message ?? string.Empty);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>コンソール出力先を利用可能にする</summary>
        private static void EnsureConsole()
        {
            if (_consoleReady)
            {
                return;
            }

            lock (ConsoleLock)
            {
                if (_consoleReady)
                {
                    return;
                }

                if (!AttachConsole(AttachParentProcess))
                {
                    AllocConsole();
                }

                try
                {
                    // Console.SetOut / SetError は同期化されたライターを設定するため、複数スレッドからの書き込みも安全
                    Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
                    Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
                }
                catch (IOException)
                {
                }

                _consoleReady = true;
            }
        }


        /********************************************************************************/
        /*                               Win32 API 定義                                 */
        /********************************************************************************/
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();
    }
}
