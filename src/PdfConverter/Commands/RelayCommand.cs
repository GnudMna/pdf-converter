using System;
using System.Windows.Input;

namespace PdfConverter.Commands
{
    /// <summary>
    /// MVVM パターンで使用する汎用コマンド実装
    /// </summary>
    /// <remarks>
    /// <see cref="ICommand"/> をデリゲートでラップし、ViewModel 側でロジックを完結させる
    /// </remarks>
    public class RelayCommand : IRelayCommand
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>コマンド実行時に呼び出されるアクション</summary>
        private readonly Action _execute;

        /// <summary>
        /// 実行可否を判定する関数<br/>
        /// <c>null</c>の場合は常に実行可能
        /// </summary>
        private readonly Func<bool> _canExecute;


        /********************************************************************************/
        /*                               イベントハンドラ                               */
        /********************************************************************************/
        /// <inheritdoc/>
        public event EventHandler CanExecuteChanged;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 同期アクションでコマンドを初期化
        /// </summary>
        /// <param name="execute">コマンド実行時に呼び出されるアクション</param>
        /// <param name="canExecute">実行可否を判定する関数<br/><c>null</c>の場合は常に実行可能</param>
        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            CommandManager.RequerySuggested += OnCommandManagerRequerySuggested;
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();

        /// <inheritdoc/>
        public void Execute(object parameter) => _execute();

        /// <inheritdoc/>
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);


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
