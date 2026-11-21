using System;
using System.Threading.Tasks;
using PdfConverter.Models;
using PdfConverter.Tests.Helpers;
using PdfConverter.ViewModels.Coordinators;
using Xunit;

namespace PdfConverter.Tests.ViewModels.Coordinators
{
    /// <summary>
    /// <see cref="PreviewUiFeedback"/> の遅延 UI フィードバックを検証する
    /// </summary>
    public class PreviewUiFeedbackTests
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        private const int TestDelayMilliseconds = 50;


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 遅延前に完了した場合は処理中 UI を出さないことを検証する
        /// </summary>
        [Fact]
        public void CompleteSuccess_BeforeActivation_DoesNotUpdateUi()
        {
            var host = new TestMainViewModelHost();
            using (var feedback = CreateFeedback(host, () => true))
            {
                feedback.CompleteSuccess();
            }

            Assert.False(host.IsBusy);
            Assert.Null(host.StatusMessage);
        }

        /// <summary>
        /// 遅延後に完了した場合は処理中 UI を解除し、成功ステータスを表示することを検証する
        /// </summary>
        [Fact]
        public async Task CompleteSuccess_AfterActivation_ClearsBusyAndSetsSuccess()
        {
            var host = new TestMainViewModelHost();
            using (var feedback = CreateFeedback(host, () => true))
            {
                await WaitForActivationAsync(host);

                feedback.CompleteSuccess("完了しました。");
            }

            Assert.False(host.IsBusy);
            Assert.Equal("完了しました。", host.StatusMessage);
            Assert.Equal(StatusKind.Success, host.StatusKind);
        }

        /// <summary>
        /// 遅延後にキャンセルした場合は処理中 UI を解除し、情報ステータスを表示することを検証する
        /// </summary>
        [Fact]
        public async Task CompleteCancelled_AfterActivation_SetsInfoStatus()
        {
            var host = new TestMainViewModelHost();
            using (var feedback = CreateFeedback(host, () => true))
            {
                await WaitForActivationAsync(host);

                feedback.CompleteCancelled();
            }

            Assert.False(host.IsBusy);
            Assert.Equal("プレビュー変換をキャンセルしました。", host.StatusMessage);
            Assert.Equal(StatusKind.Info, host.StatusKind);
        }

        /// <summary>
        /// 遅延前の失敗でもエラーステータスが設定されることを検証する
        /// </summary>
        [Fact]
        public void CompleteError_BeforeActivation_SetsErrorWithoutBusyState()
        {
            var host = new TestMainViewModelHost();
            using (var feedback = CreateFeedback(host, () => true))
            {
                feedback.CompleteError("プレビュー生成に失敗しました。");
            }

            Assert.False(host.IsBusy);
            Assert.Equal("プレビュー生成に失敗しました。", host.StatusMessage);
            Assert.Equal(StatusKind.Error, host.StatusKind);
        }

        /// <summary>
        /// 遅延後の失敗では処理中 UI を解除し、エラーステータスを表示することを検証する
        /// </summary>
        [Fact]
        public async Task CompleteError_AfterActivation_ClearsBusyAndSetsError()
        {
            var host = new TestMainViewModelHost();
            using (var feedback = CreateFeedback(host, () => true))
            {
                await WaitForActivationAsync(host);

                feedback.CompleteError("プレビュー生成に失敗しました。");
            }

            Assert.False(host.IsBusy);
            Assert.Equal("プレビュー生成に失敗しました。", host.StatusMessage);
            Assert.Equal(StatusKind.Error, host.StatusKind);
        }

        /// <summary>
        /// 操作が最新でなくなった場合は UI を更新しないことを検証する
        /// </summary>
        [Fact]
        public async Task CompleteSuccess_WhenOperationIsStale_DoesNotUpdateUi()
        {
            var host = new TestMainViewModelHost();
            bool isCurrent = true;
            using (var feedback = CreateFeedback(host, () => isCurrent))
            {
                await WaitForActivationAsync(host);
                isCurrent = false;

                feedback.CompleteSuccess();
            }

            Assert.True(host.IsBusy);
            Assert.Equal("プレビュー生成中...", host.StatusMessage);
            Assert.Equal(StatusKind.Progress, host.StatusKind);
        }

        /// <summary>
        /// 中断時に表示済みの処理中 UI を元に戻すことを検証する
        /// </summary>
        [Fact]
        public async Task Abandon_AfterActivation_ClearsBusyWhenCurrent()
        {
            var host = new TestMainViewModelHost();
            using (var feedback = CreateFeedback(host, () => true))
            {
                await WaitForActivationAsync(host);

                feedback.Abandon();
            }

            Assert.False(host.IsBusy);
        }

        /// <summary>
        /// 操作が最新でない場合の中断では処理中 UI を変更しないことを検証する
        /// </summary>
        [Fact]
        public async Task Abandon_WhenOperationIsStale_DoesNotClearBusy()
        {
            var host = new TestMainViewModelHost();
            bool isCurrent = true;
            using (var feedback = CreateFeedback(host, () => isCurrent))
            {
                await WaitForActivationAsync(host);
                isCurrent = false;

                feedback.Abandon();
            }

            Assert.True(host.IsBusy);
        }

        /// <summary>
        /// 破棄時に遅延スケジュールがキャンセルされ、処理中 UI が出ないことを検証する
        /// </summary>
        [Fact]
        public async Task Dispose_BeforeActivation_PreventsBusyUi()
        {
            var host = new TestMainViewModelHost();
            var feedback = CreateFeedback(host, () => true);
            feedback.Dispose();

            await Task.Delay(TestDelayMilliseconds + 50);

            Assert.False(host.IsBusy);
            Assert.Null(host.StatusMessage);
        }

        /// <summary>
        /// 既定の遅延時間定数が 400 ミリ秒であることを検証する
        /// </summary>
        [Fact]
        public void ActivationDelayMilliseconds_Is400()
        {
            Assert.Equal(400, PreviewUiFeedback.ActivationDelayMilliseconds);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        private static PreviewUiFeedback CreateFeedback(TestMainViewModelHost host, Func<bool> isOperationCurrent)
        {
            return new PreviewUiFeedback(host, isOperationCurrent, TestDelayMilliseconds);
        }

        private static async Task WaitForActivationAsync(TestMainViewModelHost host)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                if (host.IsBusy)
                {
                    return;
                }

                await Task.Delay(25);
            }

            throw new InvalidOperationException("処理中 UI がアクティブ化されませんでした。");
        }
    }
}
