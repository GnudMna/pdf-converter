namespace PdfConverter.Models
{
    /// <summary>
    /// Word → PDF 変換エンジン選択 ComboBox 用の表示項目
    /// </summary>
    public sealed class WordToPdfBackendOption
    {
        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>変換エンジン</summary>
        public WordToPdfBackend Backend { get; }

        /// <summary>UI 表示名</summary>
        public string DisplayName { get; }


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 指定した変換エンジンと表示名で選択肢を初期化する
        /// </summary>
        /// <param name="backend">変換エンジン</param>
        /// <param name="displayName">表示名</param>
        public WordToPdfBackendOption(WordToPdfBackend backend, string displayName)
        {
            Backend = backend;
            DisplayName = displayName;
        }
    }
}
