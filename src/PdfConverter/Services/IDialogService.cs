namespace PdfConverter.Services
{
    /// <summary>
    /// ダイアログ表示用アイコンの種類
    /// </summary>
    public enum DialogIcon
    {
        /// <summary>アイコンなし</summary>
        None,

        /// <summary>警告</summary>
        Warning,

        /// <summary>エラー</summary>
        Error,

        /// <summary>情報</summary>
        Information,
    }

    /// <summary>
    /// ファイル選択・フォルダー選択・メッセージ表示などUIダイアログの抽象化
    /// </summary>
    public interface IDialogService
    {
        /********************************************************************************/
        /*                                 抽象メソッド                                 */
        /********************************************************************************/
        /// <summary>
        /// PDFファイル選択ダイアログを表示する
        /// </summary>
        /// <returns>選択されたファイルの絶対パス。キャンセル時は <c>null</c></returns>
        string ShowOpenPdfFileDialog();

        /// <summary>
        /// フォルダー選択ダイアログを表示する
        /// </summary>
        /// <returns>選択されたフォルダーの絶対パス。キャンセル時は <c>null</c></returns>
        string ShowFolderBrowserDialog();

        /// <summary>
        /// 情報メッセージを表示する
        /// </summary>
        /// <param name="message">表示するメッセージ</param>
        /// <param name="title">ダイアログのタイトル</param>
        void ShowMessage(string message, string title = "PDF Converter");

        /// <summary>
        /// はい/いいえの確認ダイアログを表示する
        /// </summary>
        /// <param name="message">表示するメッセージ</param>
        /// <param name="title">ダイアログのタイトル</param>
        /// <param name="icon">表示するアイコン</param>
        /// <returns>「はい」が選択された場合は <c>true</c></returns>
        bool ShowYesNo(string message, string title, DialogIcon icon = DialogIcon.None);
    }
}
