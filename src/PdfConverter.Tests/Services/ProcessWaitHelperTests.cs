using System;
using System.Diagnostics;
using System.Threading;
using FluentAssertions;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="ProcessWaitHelper"/>の動作を検証する
    /// </summary>
    public class ProcessWaitHelperTests
    {
        /// <summary>
        /// タイムアウト時にプロセスを終了し<see cref="TimeoutException"/>を送出することを検証する
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

                act.Should().Throw<TimeoutException>().WithMessage("timeout");
                process.HasExited.Should().BeTrue();
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

                act.Should().Throw<OperationCanceledException>();
                process.HasExited.Should().BeTrue();
            }
        }
    }
}
