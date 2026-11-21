using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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
                // timeout.exe はコンソール入力がないと即座に終了するため、待機には ping を使う
                Arguments = "/c ping -n 61 127.0.0.1 > nul",
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (var process = Process.Start(startInfo))
            {
                Assert.False(process.HasExited, "待機対象のプロセスが待機前に終了しました。");

                Action act = () => ProcessWaitHelper.WaitForExit(
                    process,
                    TimeSpan.FromMilliseconds(500),
                    "timeout",
                    CancellationToken.None);

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
                // timeout.exe はコンソール入力がないと即座に終了するため、待機には ping を使う
                Arguments = "/c ping -n 61 127.0.0.1 > nul",
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (var cts = new CancellationTokenSource())
            using (var process = Process.Start(startInfo))
            {
                Assert.False(process.HasExited, "待機対象のプロセスが待機前に終了しました。");
                cts.CancelAfter(200);

                Action act = () => ProcessWaitHelper.WaitForExit(
                    process,
                    TimeSpan.FromMinutes(1),
                    "timeout",
                    cts.Token);

                Assert.Throws<OperationCanceledException>(act);
                Assert.True(process.HasExited);
            }
        }

        /// <summary>
        /// タイムアウト時に子プロセスも終了し、標準出力のパイプが閉じられることを検証する
        /// </summary>
        [Fact]
        public async Task WaitForExit_TimesOut_KillsChildProcesses()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c ping -n 61 127.0.0.1",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };

            using (var process = Process.Start(startInfo))
            {
                Task<string> readTask = process.StandardOutput.ReadToEndAsync(cancellationToken: TestContext.Current.CancellationToken);

                Action act = () => ProcessWaitHelper.WaitForExit(
                    process,
                    TimeSpan.FromMilliseconds(500),
                    "timeout",
                    CancellationToken.None);

                Assert.Throws<TimeoutException>(act);
                Task completed = await Task.WhenAny(
                    readTask,
                    Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
                Assert.True(completed == readTask, "子プロセスが残り、標準出力のパイプが閉じられませんでした。");
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
                    "timeout",
                    CancellationToken.None);

                Assert.True(process.HasExited);
                Assert.Equal(0, process.ExitCode);
                Assert.Contains("stdout line 1", captured.StandardOutput);
                Assert.Contains("stderr line 1", captured.StandardError);
            }
        }
    }
}
