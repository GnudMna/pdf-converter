using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="StaTaskRunner"/> の動作を検証する
    /// </summary>
    public class StaTaskRunnerTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// キャンセル時にクリーンアップ処理が呼び出されることを検証する
        /// </summary>
        [Fact]
        public async Task RunAsync_Cancellation_InvokesCancelCleanup()
        {
            using (var cts = new CancellationTokenSource())
            {
                var cleanupInvoked = false;

                Task<int> task = StaTaskRunner.RunAsync(
                    _ =>
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(10));
                        return 1;
                    },
                    cts.Token,
                    () => cleanupInvoked = true);

                cts.CancelAfter(50);

                Func<Task> act = async () => await task.ConfigureAwait(false);
                await act.Should().ThrowAsync<OperationCanceledException>();
                cleanupInvoked.Should().BeTrue();
            }
        }
    }
}
