using System.Windows.Input;
using PdfConverter.Models;

namespace PdfConverter.ViewModels
{
    /// <summary>
    /// メインウィンドウが依存するViewModelのインターフェース
    /// </summary>
    public interface IMainWindowViewModel
    {
        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>処理中かどうか</summary>
        bool IsBusy { get; set; }

        /// <summary>ドラッグ&amp;ドロップオーバーレイを表示するかどうか</summary>
        bool IsDropOverlayVisible { get; set; }

        /// <summary>パス入力欄のフォーカス喪失時にPDFを読み込むコマンド</summary>
        ICommand LoadPdfFromPathCommand { get; }


        /********************************************************************************/
        /*                                 抽象メソッド                                 */
        /********************************************************************************/
        /// <summary>ドロップされたPDFファイルを読み込む</summary>
        /// <param name="filePath">PDF ファイルのパス</param>
        void HandleDroppedPdf(string filePath);

        /// <summary>ステータスメッセージと表示種類をまとめて設定する</summary>
        /// <param name="message">表示するメッセージ</param>
        /// <param name="kind">表示種類</param>
        void SetStatus(string message, StatusKind kind = StatusKind.Info);
    }
}
