using System.Windows.Input;

namespace PdfConverter.Commands
{
    /// <summary>
    /// <see cref="CanExecuteChanged"/>を手動で発火できるコマンド
    /// </summary>
    public interface IRelayCommand : ICommand
    {
        /********************************************************************************/
        /*                                 抽象メソッド                                 */
        /********************************************************************************/
        /// <summary>コマンドの実行可否の再評価をUIに通知する</summary>
        void RaiseCanExecuteChanged();
    }
}
