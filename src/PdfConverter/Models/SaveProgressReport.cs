namespace PdfConverter.Models
{
    /// <summary>
    /// PDFページの一括保存処理における進捗情報を保持する不変データオブジェクト
    /// </summary>
    /// <remarks>
    /// <see cref="System.IProgress{T}"/> を通じて UI スレッドへ進捗を通知するために使用する
    /// </remarks>
    public sealed class SaveProgressReport
    {
        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>
        /// 処理の完了率(0.0 ～ 100.0)
        /// </summary>
        public double Percentage { get; }

        /// <summary>
        /// UI に表示するステータスメッセージ(例: "保存中... 3/10 ページ")
        /// </summary>
        public string Message { get; }


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 進捗情報を初期化する
        /// </summary>
        /// <param name="percentage">完了率(0.0 ～ 100.0)</param>
        /// <param name="message">UI に表示するメッセージ</param>
        public SaveProgressReport(double percentage, string message)
        {
            Percentage = percentage;
            Message = message;
        }
    }
}
