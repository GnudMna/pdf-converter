using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PdfConverter.ConsoleLauncher
{
    /// <summary>
    /// PDF Converter 本体 (GUI アプリ) をコンソールから呼び出すための起動プログラム<br/>
    /// 本体の終了を待ち、終了コードをそのまま返す。標準入出力は本体へ引き継ぐ
    /// </summary>
    internal static class Program
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>本体の実行ファイル名</summary>
        private const string AppFileName = "PDF Converter.exe";

        /// <summary>本体を起動できなかったときの終了コード (本体の変換エラーと同じ値)</summary>
        private const int LaunchFailedExitCode = 1;


        /********************************************************************************/
        /*                              プライベートメソッド                              */
        /********************************************************************************/
        /// <summary>入力を最後まで出力へコピーし、出力を閉じる (相手が先に終了していて書き込めない場合は静かに終える)</summary>
        /// <param name="source">コピー元</param>
        /// <param name="destination">コピー先</param>
        private static void CopyAll(Stream source, Stream destination)
        {
            try
            {
                source.CopyTo(destination);
                destination.Flush();
                destination.Dispose();
            }
            catch (IOException)
            {
            }
        }

        /// <summary>エントリポイント</summary>
        /// <param name="args">起動引数 (そのまま本体へ渡す)</param>
        /// <returns>本体の終了コード</returns>
        private static int Main(string[] args)
        {
            string appPath = Path.Combine(AppContext.BaseDirectory, AppFileName);
            if (!File.Exists(appPath))
            {
                Console.Error.WriteLine($"{AppFileName} が見つかりません: {appPath}");
                return LaunchFailedExitCode;
            }

            // Ctrl+C は本体も同じコンソールで受け取り、実行中の Word などを片付けてから終了する
            // この起動プログラムが先に終了すると、片付けの途中でコマンドプロンプトへ戻ってしまうため、本体の終了を待つ
            Console.CancelKeyPress += (_, e) => e.Cancel = true;

            // 本体は GUI アプリのため、コンソールの標準入出力は引き継がれない
            // 3 本とも中継して、リダイレクトやパイプ (パスワードの標準入力渡しなど) が本体へ届くようにする
            var startInfo = new ProcessStartInfo(appPath)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (string arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            try
            {
                using Process? process = Process.Start(startInfo);
                if (process == null)
                {
                    return LaunchFailedExitCode;
                }

                Task standardOutput = Task.Run(() => CopyAll(process.StandardOutput.BaseStream, Console.OpenStandardOutput()));
                Task standardError = Task.Run(() => CopyAll(process.StandardError.BaseStream, Console.OpenStandardError()));

                // 標準入力は、入力が続く限り読み続けて閉じられないため、終了待ちの対象にしない (本体の終了でプロセスごと終わる)
                var standardInput = new Thread(() => CopyAll(Console.OpenStandardInput(), process.StandardInput.BaseStream))
                {
                    IsBackground = true,
                };
                standardInput.Start();

                process.WaitForExit();
                Task.WaitAll(standardOutput, standardError);
                return process.ExitCode;
            }
            catch (Win32Exception ex)
            {
                Console.Error.WriteLine($"{AppFileName} を起動できませんでした: {ex.Message}");
                return LaunchFailedExitCode;
            }
        }
    }
}
