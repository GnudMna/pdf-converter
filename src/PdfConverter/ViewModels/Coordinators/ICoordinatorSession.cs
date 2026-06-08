using System.Threading;

using PdfConverter.Models;

namespace PdfConverter.ViewModels.Coordinators
{
    /// <summary>
    /// Coordinator が操作セッション (キャンセル・進捗・ステータス) を制御するためのインターフェース
    /// </summary>
    public interface ICoordinatorSession
    {
        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>処理中かどうか</summary>
        bool IsBusy { get; set; }

        /// <summary>保存中かどうか</summary>
        bool IsSaving { get; set; }

        /// <summary>進捗値</summary>
        double ProgressValue { get; set; }


        /********************************************************************************/
        /*                                 抽象メソッド                                 */
        /********************************************************************************/
        /// <summary>キャンセル処理を準備する</summary>
        void PrepareCancellation();

        /// <summary>キャンセルトークンを取得する</summary>
        CancellationToken GetCancellationToken();

        /// <summary>キャンセルトークンを破棄する</summary>
        void DisposeCancellation();

        /// <summary>ステータスメッセージと表示種類をまとめて設定する</summary>
        /// <param name="message">表示するメッセージ</param>
        /// <param name="kind">表示種類</param>
        void SetStatus(string message, StatusKind kind = StatusKind.Info);

        /// <summary>フィールド検証メッセージをクリアする</summary>
        void ClearFieldValidationMessages();

        /// <summary>ナビゲーション可能なコマンドの実行可能状態を更新する</summary>
        void RaiseNavigationCanExecuteChanged();

        /// <summary>アクション可能なコマンドの実行可能状態を更新する</summary>
        void RaiseActionCanExecuteChanged();
    }
}
