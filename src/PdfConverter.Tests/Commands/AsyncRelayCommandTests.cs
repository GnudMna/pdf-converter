using System;
using System.Threading.Tasks;
using FluentAssertions;
using PdfConverter.Commands;
using PdfConverter.Tests.Helpers;
using Xunit;

namespace PdfConverter.Tests.Commands
{
    /// <summary>
    /// <see cref="AsyncRelayCommand"/> の非同期コマンド実行と再入防止を検証する
    /// </summary>
    public class AsyncRelayCommandTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// Execute 呼び出し時に登録した非同期アクションが完了まで実行されることを検証する
        /// </summary>
        [Fact]
        public void Execute_RunsAsyncAction()
        {
            StaTestHelper.Run(() =>
            {
                var executed = false;
                var command = new AsyncRelayCommand(async () =>
                {
                    await Task.Delay(10);
                    executed = true;
                });

                command.Execute(null);
                Task.Delay(100).GetAwaiter().GetResult();

                executed.Should().BeTrue();
            });
        }

        /// <summary>
        /// 非同期処理の実行中は CanExecute が false となり再入が防止されることを検証する
        /// </summary>
        [Fact]
        public void CanExecute_WhileExecuting_ReturnsFalse()
        {
            StaTestHelper.Run(() =>
            {
                var tcs = new TaskCompletionSource<bool>();
                var command = new AsyncRelayCommand(async () => await tcs.Task);

                command.Execute(null);
                command.CanExecute(null).Should().BeFalse();

                tcs.SetResult(true);
            });
        }

        /// <summary>
        /// CanExecute 述語が false の場合にコマンドが実行不可であることを検証する
        /// </summary>
        [Fact]
        public void CanExecute_WithFalsePredicate_ReturnsFalse()
        {
            StaTestHelper.Run(() =>
            {
                var command = new AsyncRelayCommand(async () => await Task.CompletedTask, () => false);

                command.CanExecute(null).Should().BeFalse();
            });
        }

        /// <summary>
        /// キャンセル例外の場合は例外ハンドラが呼ばれないことを検証する
        /// </summary>
        [Fact]
        public void Execute_WhenCanceled_DoesNotInvokeExceptionHandler()
        {
            StaTestHelper.Run(() =>
            {
                var handlerInvoked = false;
                var command = new AsyncRelayCommand(
                    () => throw new OperationCanceledException(),
                    onException: _ => handlerInvoked = true);

                command.Execute(null);
                Task.Delay(100).GetAwaiter().GetResult();

                handlerInvoked.Should().BeFalse();
                command.CanExecute(null).Should().BeTrue();
            });
        }

        /// <summary>
        /// 非同期アクションが例外をスローした場合にハンドラが呼ばれ、実行状態がリセットされることを検証する
        /// </summary>
        [Fact]
        public void Execute_WhenActionThrows_InvokesExceptionHandlerAndResetsExecuting()
        {
            StaTestHelper.Run(() =>
            {
                Exception caught = null;
                var command = new AsyncRelayCommand(
                    async () =>
                    {
                        await Task.CompletedTask;
                        throw new InvalidOperationException("test");
                    },
                    onException: ex => caught = ex);

                command.Execute(null);
                Task.Delay(100).GetAwaiter().GetResult();

                caught.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("test");
                command.CanExecute(null).Should().BeTrue();
            });
        }

        /// <summary>
        /// 非同期アクションに null を渡した場合に ArgumentNullException がスローされることを検証する
        /// </summary>
        [Fact]
        public void Constructor_NullExecute_ThrowsArgumentNullException()
        {
            StaTestHelper.Run(() =>
            {
                Action act = () => new AsyncRelayCommand(null);

                act.Should().Throw<ArgumentNullException>();
            });
        }
    }
}
