using System.Windows.Media.Imaging;

using PdfConverter.Models;

namespace PdfConverter.ViewModels.Coordinators
{
    /// <summary>
    /// <see cref="IPdfPreviewCoordinator"/> がプレビュー操作に必要とする ViewModel 状態
    /// </summary>
    public interface IPreviewCoordinatorHost : ICoordinatorSession, IResolutionInputHost
    {
        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>読み込むドキュメントのパス</summary>
        string? FilePath { get; set; }

        /// <summary>読み込み済みドキュメントのパス</summary>
        string? LoadedFilePath { get; set; }

        /// <summary>表示ページ番号の入力値</summary>
        string PageNumber { get; set; }

        /// <summary>PDF のページ数</summary>
        int PageCount { get; set; }

        /// <summary>プレビュー画像</summary>
        BitmapSource? PreviewImage { get; set; }

        /// <summary>透明度を保持するかどうか</summary>
        bool PreserveTransparency { get; }

        /// <summary>表示ページ番号の入力エラーメッセージ</summary>
        string? PageNumberValidationMessage { get; set; }

        /// <summary>現在表示中のページ番号</summary>
        int CurrentPreviewPage { get; }

        /// <summary>プレビュー画像を表示する領域の幅 (デバイス px、未確定の場合は 0)</summary>
        int PreviewViewportPixelWidth { get; }

        /// <summary>プレビュー画像を表示する領域の高さ (デバイス px、未確定の場合は 0)</summary>
        int PreviewViewportPixelHeight { get; }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// パスワードで保護された PDF を開くためのパスワードを利用者に入力させる
        /// </summary>
        /// <param name="filePath">開こうとしているドキュメントのパス</param>
        /// <param name="previousPasswordWasIncorrect">直前に指定したパスワードが正しくなかったかどうか</param>
        /// <returns>入力されたパスワード。キャンセルされた場合は <c>null</c></returns>
        string? RequestPdfPassword(string filePath, bool previousPasswordWasIncorrect);
    }
}
