namespace PdfConverter.Models
{
    /// <summary>
    /// 出力形式のComboBox表示用モデル
    /// </summary>
    public sealed class OutputImageFormatOption
    {
        /********************************************************************************/
        /*                                 プロパティ                                  */
        /********************************************************************************/
        /// <summary>出力画像形式</summary>
        public OutputImageFormat Format { get; }

        /// <summary>UIに表示するラベル</summary>
        public string DisplayName { get; }


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 指定した出力形式と表示名で選択肢を初期化する
        /// </summary>
        /// <param name="format">出力形式</param>
        /// <param name="displayName">表示名</param>
        public OutputImageFormatOption(OutputImageFormat format, string displayName)
        {
            Format = format;
            DisplayName = displayName;
        }
    }
}