using System;
using System.Windows.Input;
using PdfConverter.Models;

namespace PdfConverter.ViewModels
{
    /// <summary>
    /// メインウィンドウが依存する ViewModel のインターフェース
    /// </summary>
    public interface IMainWindowViewModel
    {
        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>処理中かどうか</summary>
        bool IsBusy { get; set; }

        /// <summary>プログレスバーを表示するかどうか</summary>
        bool IsProgressBarVisible { get; set; }

        /// <summary>ドラッグ&amp;ドロップオーバーレイを表示するかどうか</summary>
        bool IsDropOverlayVisible { get; set; }

        /// <summary>パス入力欄のフォーカス喪失時に PDF を読み込むコマンド</summary>
        ICommand LoadPdfFromPathCommand { get; }


        /********************************************************************************/
        /*                                 抽象メソッド                                 */
        /********************************************************************************/
        /// <summary>ドロップされたドキュメントファイルを読み込む</summary>
        /// <param name="filePath">ドキュメントファイルのパス</param>
        void HandleDroppedDocument(string filePath);

        /// <summary>
        /// 入力欄に入力途中の値を反映する<br/>
        /// 続けてドキュメントを読み込む前に使い、反映による読み込み済みドキュメントの再読み込み・再描画は行わない
        /// </summary>
        /// <param name="commitInput">入力途中の値を反映する処理</param>
        void CommitPendingInput(Action commitInput);

        /// <summary>実行中の変換・保存処理にキャンセルを要求する (処理中でなければ何もしない)</summary>
        void CancelCurrentOperation();

        /// <summary>複数のファイルがドロップされ、1 件だけを読み込んだことを利用者へ通知する</summary>
        /// <param name="droppedCount">ドロップされたファイルの総数</param>
        /// <param name="loadedFilePath">読み込んだドキュメントファイルのパス</param>
        void NotifyMultipleFilesDropped(int droppedCount, string loadedFilePath);

        /// <summary>ステータスメッセージと表示種類をまとめて設定する</summary>
        /// <param name="message">表示するメッセージ</param>
        /// <param name="kind">表示種類</param>
        void SetStatus(string message, StatusKind kind = StatusKind.Info);
    }
}
