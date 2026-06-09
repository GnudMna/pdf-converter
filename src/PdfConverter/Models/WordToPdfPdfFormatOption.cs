namespace PdfConverter.Models
{
    /// <summary>
    /// Word → PDF 変換時の PDF 形式選択 ComboBox 用の表示項目
    /// </summary>
    public sealed class WordToPdfPdfFormatOption
    {
        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>PDF 形式</summary>
        public WordToPdfPdfFormat Format { get; }

        /// <summary>UI 表示名</summary>
        public string DisplayName { get; }


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 指定した PDF 形式と表示名で選択肢を初期化する
        /// </summary>
        /// <param name="format">PDF 形式</param>
        /// <param name="displayName">表示名</param>
        public WordToPdfPdfFormatOption(WordToPdfPdfFormat format, string displayName)
        {
            Format = format;
            DisplayName = displayName;
        }
    }
}
