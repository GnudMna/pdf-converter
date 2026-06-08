using System.Threading;
using System.Windows.Media.Imaging;
using PdfConverter.Models;

namespace PdfConverter.ViewModels
{
    /// <summary>
    /// Coordinator が MainViewModel の状態を読み書きするためのインターフェース
    /// </summary>
    public interface IMainViewModelHost
    {
        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>読み込んだ PDF のパス</summary>
        string FilePath { get; set; }

        /// <summary>読み込んだ PDF のパス</summary>
        string LoadedFilePath { get; set; }

        /// <summary>現在表示中のページ番号</summary>
        string PageNumber { get; set; }

        /// <summary>保存するページの範囲</summary>
        string PageRange { get; }

        /// <summary>PDF のページ数</summary>
        int PageCount { get; set; }

        /// <summary>プレビュー画像</summary>
        BitmapSource PreviewImage { get; set; }

        /// <summary>解像度の指定方法</summary>
        ResolutionMode ResolutionMode { get; }

        /// <summary>解像度の値</summary>
        string ResolutionValue { get; }

        /// <summary>透明度を保持するかどうか</summary>
        bool PreserveTransparency { get; }

        /// <summary>全ページを保存するかどうか</summary>
        bool IsAllPagesSelected { get; }

        /// <summary>出力画像形式</summary>
        OutputImageFormat OutputImageFormat { get; }

        /// <summary>処理中かどうか</summary>
        bool IsBusy { get; set; }

        /// <summary>保存中かどうか</summary>
        bool IsSaving { get; set; }

        /// <summary>ステータスメッセージ</summary>
        string StatusMessage { get; set; }

        /// <summary>ステータスバーの表示種類</summary>
        StatusKind StatusKind { get; set; }

        /// <summary>出力解像度の入力エラーメッセージ</summary>
        string ResolutionValidationMessage { get; set; }

        /// <summary>保存ページ範囲の入力エラーメッセージ</summary>
        string PageRangeValidationMessage { get; set; }

        /// <summary>表示ページ番号の入力エラーメッセージ</summary>
        string PageNumberValidationMessage { get; set; }

        /// <summary>進捗値</summary>
        double ProgressValue { get; set; }

        /// <summary>現在表示中のページ番号</summary>
        int CurrentPreviewPage { get; }


        /********************************************************************************/
        /*                                 抽象メソッド                                 */
        /********************************************************************************/
        /// <summary>キャンセル処理を準備する</summary>
        void PrepareCancellation();

        /// <summary>キャンセルトークンを取得する</summary>
        CancellationToken GetCancellationToken();

        /// <summary>キャンセルトークンを破棄する</summary>
        void DisposeCancellation();

        /// <summary>ナビゲーション可能なコマンドの実行可能状態を更新する</summary>
        void RaiseNavigationCanExecuteChanged();

        /// <summary>アクション可能なコマンドの実行可能状態を更新する</summary>
        void RaiseActionCanExecuteChanged();

        /// <summary>ステータスメッセージと表示種類をまとめて設定する</summary>
        /// <param name="message">表示するメッセージ</param>
        /// <param name="kind">表示種類</param>
        void SetStatus(string message, StatusKind kind = StatusKind.Info);

        /// <summary>フィールド検証メッセージをクリアする</summary>
        void ClearFieldValidationMessages();
    }
}
