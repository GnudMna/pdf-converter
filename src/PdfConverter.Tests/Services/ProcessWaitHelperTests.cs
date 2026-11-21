using System;
using System.Diagnostics;
using System.Threading;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="ProcessWaitHelper"/> の動作を検証する
    /// </summary>
    public class ProcessWaitHelperTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// タイムアウト時にプロセスを終了し <see cref="TimeoutException"/> を送出することを検証する
        /// </summary>
        [Fact]
        public void WaitForExit_TimesOut_KillsProcess()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c timeout /t 60 /nobreak",
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (var process = Process.Start(startInfo))
            {
                Action act = () => ProcessWaitHelper.WaitForExit(
                    process,
                    TimeSpan.FromMilliseconds(500),
                    CancellationToken.None,
                    "timeout");

                var ex = Assert.Throws<TimeoutException>(act);
                Assert.Contains("timeout", ex.Message);
                Assert.True(process.HasExited);
            }
        }

        /// <summary>
        /// キャンセル時にプロセスを終了し OperationCanceledException を送出することを検証する
        /// </summary>
        [Fact]
        public void WaitForExit_Cancellation_KillsProcess()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c timeout /t 60 /nobreak",
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (var cts = new CancellationTokenSource())
            using (var process = Process.Start(startInfo))
            {
                cts.CancelAfter(200);

                Action act = () => ProcessWaitHelper.WaitForExit(
                    process,
                    TimeSpan.FromMinutes(1),
                    cts.Token,
                    "timeout");

                Assert.Throws<OperationCanceledException>(act);
                Assert.True(process.HasExited);
            }
        }

        /// <summary>
        /// 大量の標準出力 / 標準エラー出力でもデッドロックせず終了することを検証する
        /// </summary>
        [Fact]
        public void WaitForExitWithCapturedOutput_VerboseProcess_CapturesOutputWithoutDeadlock()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c (for /l %i in (1,1,500) do @echo stdout line %i) & (for /l %i in (1,1,500) do @echo stderr line %i 1>&2)",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();

                CapturedProcessOutput captured = ProcessWaitHelper.WaitForExitWithCapturedOutput(
                    process,
                    TimeSpan.FromSeconds(30),
                    CancellationToken.None,
                    "timeout");

                Assert.True(process.HasExited);
                Assert.Equal(0, process.ExitCode);
                Assert.Contains("stdout line 1", captured.StandardOutput);
                Assert.Contains("stderr line 1", captured.StandardError);
            }
        }
    }
}
