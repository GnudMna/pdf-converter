using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using PdfConverter.Models;

namespace PdfConverter.ViewModels.Coordinators
{
    /// <summary>
    /// プレビュー切り替え時の UI 更新を遅延させ、短時間で完了する操作ではチラつきを抑える
    /// </summary>
    internal sealed class PreviewUiFeedback : IDisposable
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>処理中表示を出すまでの待機時間 (ミリ秒)</summary>
        public const int ActivationDelayMilliseconds = 400;


        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>UI 状態を更新するセッション</summary>
        private readonly ICoordinatorSession _session;

        /// <summary>操作が最新かどうかを返す関数</summary>
        private readonly Func<bool> _isOperationCurrent;

        /// <summary>処理中表示を出すまでの待機時間 (ミリ秒)</summary>
        private readonly int _delayMilliseconds;

        /// <summary>処理中表示の待機を取り消すためのソース (待機の終了後は <c>null</c>)</summary>
        private CancellationTokenSource? _delayCancellation;

        /// <summary>処理中表示を出したかどうか</summary>
        private bool _isActivated;

        /// <summary>破棄済み、または中断済みかどうか</summary>
        private bool _isDisposed;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 指定したセッションに対して遅延 UI フィードバックを開始する
        /// </summary>
        /// <param name="session">UI 状態を更新するセッション</param>
        /// <param name="isOperationCurrent">操作が最新かどうかを返す関数</param>
        /// <param name="delayMilliseconds">処理中表示を出すまでの待機時間 (ミリ秒)</param>
        public PreviewUiFeedback(
            ICoordinatorSession session,
            Func<bool> isOperationCurrent,
            int delayMilliseconds = ActivationDelayMilliseconds)
        {
            _session = session;
            _isOperationCurrent = isOperationCurrent;
            _delayMilliseconds = delayMilliseconds;
            ScheduleActivation();
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>処理が正常に完了したときの UI を更新する</summary>
        /// <param name="message">完了メッセージ</param>
        public void CompleteSuccess(string message = "プレビューを更新しました。")
        {
            if (_isDisposed || !_isOperationCurrent())
            {
                return;
            }

            CancelSchedule();
            if (!_isActivated)
            {
                return;
            }

            _session.IsBusy = false;
            _session.SetStatus(message, StatusKind.Success);
        }

        /// <summary>処理がキャンセルされたときの UI を更新する</summary>
        public void CompleteCancelled()
        {
            if (_isDisposed || !_isOperationCurrent())
            {
                return;
            }

            CancelSchedule();
            if (!_isActivated)
            {
                return;
            }

            _session.IsBusy = false;
            _session.SetStatus("プレビュー変換をキャンセルしました。", StatusKind.Info);
        }

        /// <summary>処理が失敗したときの UI を更新する</summary>
        /// <param name="message">エラーメッセージ</param>
        public void CompleteError(string message)
        {
            if (_isDisposed || !_isOperationCurrent())
            {
                return;
            }

            CancelSchedule();
            if (_isActivated)
            {
                _session.IsBusy = false;
            }

            _session.SetStatus(message, StatusKind.Error);
        }

        /// <summary>
        /// 操作を中断し、表示済みの処理中 UI を元に戻す
        /// </summary>
        public void Abandon()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            CancelSchedule();
            if (_isActivated && _isOperationCurrent())
            {
                _session.IsBusy = false;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            CancelSchedule();
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>遅延 UI フィードバックをスケジュールする</summary>
        private void ScheduleActivation()
        {
            _delayCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = _delayCancellation.Token;

            Task.Run(
                async () =>
                {
                    try
                    {
                        await Task.Delay(_delayMilliseconds, cancellationToken).ConfigureAwait(false);
                        RunOnUiThread(Activate);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                },
                cancellationToken);
        }

        /// <summary>UI フィードバックをアクティブ化する</summary>
        private void Activate()
        {
            if (_isDisposed || !_isOperationCurrent())
            {
                return;
            }

            _isActivated = true;
            _session.IsBusy = true;
            _session.SetStatus("プレビュー生成中...", StatusKind.Progress);
        }

        /// <summary>遅延 UI フィードバックのスケジュールをキャンセルする</summary>
        private void CancelSchedule()
        {
            if (_delayCancellation == null)
            {
                return;
            }

            _delayCancellation.Cancel();
            _delayCancellation.Dispose();
            _delayCancellation = null;
        }

        /// <summary>UI スレッドで処理を実行する (Dispatcher がない、または既に UI スレッドの場合は直接実行する)</summary>
        /// <param name="action">実行する処理</param>
        private static void RunOnUiThread(Action action)
        {
            Dispatcher? dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.Invoke(action);
        }
    }
}
