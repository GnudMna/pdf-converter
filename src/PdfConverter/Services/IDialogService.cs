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
    /// ファイル選択・フォルダー選択・メッセージ表示など UI ダイアログの抽象化
    /// </summary>
    public interface IDialogService
    {
        /********************************************************************************/
        /*                                 抽象メソッド                                 */
        /********************************************************************************/
        /// <summary>
        /// ドキュメントファイル選択ダイアログを表示する
        /// </summary>
        /// <returns>選択されたファイルの絶対パス<br/>キャンセル時は <c>null</c></returns>
        string? ShowOpenDocumentFileDialog();

        /// <summary>
        /// フォルダー選択ダイアログを表示する
        /// </summary>
        /// <returns>選択されたフォルダーの絶対パス<br/>キャンセル時は <c>null</c></returns>
        string? ShowFolderBrowserDialog();

        /// <summary>
        /// PDF ファイル保存ダイアログを表示する
        /// </summary>
        /// <param name="suggestedFileName">初期表示するファイル名</param>
        /// <returns>選択されたファイルの絶対パス<br/>キャンセル時は <c>null</c></returns>
        string? ShowSavePdfFileDialog(string suggestedFileName);

        /// <summary>
        /// はい / いいえの確認ダイアログを表示する
        /// </summary>
        /// <param name="message">表示するメッセージ</param>
        /// <param name="title">ダイアログのタイトル</param>
        /// <param name="icon">表示するアイコン</param>
        /// <param name="yesText">肯定ボタンのラベル</param>
        /// <param name="noText">否定ボタンのラベル</param>
        /// <returns>true: 肯定ボタンが選択された / false: 否定ボタンが選択された</returns>
        bool ShowYesNo(
            string message,
            string title,
            DialogIcon icon = DialogIcon.None,
            string yesText = "はい",
            string noText = "いいえ");

        /// <summary>
        /// OK のみのメッセージダイアログを表示する
        /// </summary>
        /// <param name="message">表示するメッセージ</param>
        /// <param name="title">ダイアログのタイトル</param>
        /// <param name="icon">表示するアイコン</param>
        /// <param name="okText">OK ボタンのラベル</param>
        void ShowMessage(
            string message,
            string title,
            DialogIcon icon = DialogIcon.Information,
            string okText = "OK");

        /// <summary>
        /// パスワード入力ダイアログを表示する
        /// </summary>
        /// <param name="message">表示するメッセージ</param>
        /// <param name="title">ダイアログのタイトル</param>
        /// <returns>入力されたパスワード<br/>キャンセル時は <c>null</c></returns>
        string? ShowPasswordDialog(string message, string title);

        /// <summary>
        /// バージョン情報とオープンソースクレジットのダイアログを表示する
        /// </summary>
        void ShowAbout();

        /// <summary>
        /// 利用者向けドキュメントを既定のブラウザーで開く
        /// </summary>
        /// <remarks>
        /// ドキュメントが同梱されていない場合 (開発ビルドなど) は、その旨を表示する
        /// </remarks>
        void OpenUserDocs();
    }
}
