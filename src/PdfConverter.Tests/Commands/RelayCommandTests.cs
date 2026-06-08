using System;
using FluentAssertions;
using PdfConverter.Commands;
using PdfConverter.Tests.Helpers;
using Xunit;

namespace PdfConverter.Tests.Commands
{
    /// <summary>
    /// <see cref="RelayCommand"/> の同期コマンド実行と実行可否判定を検証する
    /// </summary>
    public class RelayCommandTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// Execute 呼び出し時に登録したアクションが実行されることを検証する
        /// </summary>
        [Fact]
        public void Execute_InvokesAction()
        {
            StaTestHelper.Run(() =>
            {
                var executed = false;
                var command = new RelayCommand(() => executed = true);

                command.Execute(null);

                executed.Should().BeTrue();
            });
        }

        /// <summary>
        /// CanExecute 述語を指定しない場合、常に実行可能であることを検証する
        /// </summary>
        [Fact]
        public void CanExecute_WithoutPredicate_ReturnsTrue()
        {
            StaTestHelper.Run(() =>
            {
                var command = new RelayCommand(() => { });

                command.CanExecute(null).Should().BeTrue();
            });
        }

        /// <summary>
        /// CanExecute 述語の戻り値がコマンドの実行可否に反映されることを検証する
        /// </summary>
        [Fact]
        public void CanExecute_WithPredicate_ReflectsPredicateResult()
        {
            StaTestHelper.Run(() =>
            {
                var enabled = false;
                var command = new RelayCommand(() => { }, () => enabled);

                command.CanExecute(null).Should().BeFalse();

                enabled = true;
                command.CanExecute(null).Should().BeTrue();
            });
        }

        /// <summary>
        /// 実行アクションに null を渡した場合に ArgumentNullException がスローされることを検証する
        /// </summary>
        [Fact]
        public void Constructor_NullExecute_ThrowsArgumentNullException()
        {
            StaTestHelper.Run(() =>
            {
                Action act = () => new RelayCommand(null);

                act.Should().Throw<ArgumentNullException>();
            });
        }

        /// <summary>
        /// RaiseCanExecuteChanged 呼び出しで CanExecuteChanged イベントが発火することを検証する
        /// </summary>
        [Fact]
        public void RaiseCanExecuteChanged_NotifiesSubscribers()
        {
            StaTestHelper.Run(() =>
            {
                var command = new RelayCommand(() => { });
                var notified = false;
                command.CanExecuteChanged += (_, __) => notified = true;

                command.RaiseCanExecuteChanged();

                notified.Should().BeTrue();
            });
        }
    }
}
