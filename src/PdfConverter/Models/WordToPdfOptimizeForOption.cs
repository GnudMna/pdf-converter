namespace PdfConverter.Models
{
    /// <summary>
    /// Word → PDF 変換時の出力最適化モード選択 ComboBox 用の表示項目
    /// </summary>
    public sealed class WordToPdfOptimizeForOption
    {
        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>出力最適化モード</summary>
        public WordToPdfOptimizeFor OptimizeFor { get; }

        /// <summary>UI 表示名</summary>
        public string DisplayName { get; }


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 指定した出力最適化モードと表示名で選択肢を初期化する
        /// </summary>
        /// <param name="optimizeFor">出力最適化モード</param>
        /// <param name="displayName">表示名</param>
        public WordToPdfOptimizeForOption(WordToPdfOptimizeFor optimizeFor, string displayName)
        {
            OptimizeFor = optimizeFor;
            DisplayName = displayName;
        }
    }
}
