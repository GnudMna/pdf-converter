using System;
using System.Threading.Tasks;
using System.Windows.Input;
using PdfConverter.Services;

namespace PdfConverter.Commands
{
    /// <summary>
    /// 非同期処理を実行するコマンド<br/>
    /// 実行中は再入を防止し、完了まで <see cref="CanExecute"/> を <c>false</c> にする
    /// </summary>
    public class AsyncRelayCommand : IRelayCommand
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>コマンド実行時に呼び出される非同期アクション</summary>
        private readonly Func<Task> _execute;

        /// <summary>
        /// 実行可否を判定する関数<br/>
        /// <c>null</c>の場合は常に実行可能
        /// </summary>
        private readonly Func<bool> _canExecute;

        /// <summary>
        /// 非同期処理で発生した未処理例外を受け取るコールバック<br/>
        /// <c>null</c>の場合は無視する
        /// </summary>
        private readonly Action<Exception> _onException;

        /// <summary>非同期処理が実行中かどうか</summary>
        private bool _isExecuting;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 非同期アクションでコマンドを初期化する
        /// </summary>
        /// <param name="execute">コマンド実行時に呼び出される非同期アクション</param>
        /// <param name="canExecute">実行可否を判定する関数<br/><c>null</c>の場合は常に実行可能</param>
        /// <param name="onException">非同期処理で発生した未処理例外を受け取るコールバック<br/><c>null</c>の場合は無視する</param>
        public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute = null, Action<Exception> onException = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _onException = onException;
            CommandManager.RequerySuggested += OnCommandManagerRequerySuggested;
        }


        /********************************************************************************/
        /*                               イベントハンドラ                               */
        /********************************************************************************/
        /// <inheritdoc/>
        public event EventHandler CanExecuteChanged;


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        public bool CanExecute(object parameter)
        {
            if (_isExecuting)
            {
                return false;
            }

            return _canExecute == null || _canExecute();
        }

        /// <inheritdoc/>
        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter))
            {
                return;
            }

            _isExecuting = true;
            RaiseCanExecuteChanged();

            try
            {
                await _execute();
            }
            catch (Exception ex) when (CancellationExceptionHelper.IsOrContainsCancellation(ex))
            {
            }
            catch (Exception ex)
            {
                _onException?.Invoke(ex);
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        /// <inheritdoc/>
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// <see cref="CommandManager.RequerySuggested"/>発火時に実行可否の再評価を通知する
        /// </summary>
        private void OnCommandManagerRequerySuggested(object sender, EventArgs e)
        {
            RaiseCanExecuteChanged();
        }
    }
}
