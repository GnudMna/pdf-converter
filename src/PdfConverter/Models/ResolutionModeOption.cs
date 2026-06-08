namespace PdfConverter.Models
{
    /// <summary>
    /// 解像度モードの ComboBox 表示用モデル
    /// </summary>
    public class ResolutionModeOption
    {
        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>解像度の指定方法</summary>
        public ResolutionMode Mode { get; }

        /// <summary>UI に表示するラベル</summary>
        public string DisplayName { get; }


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 指定したモードと表示名で選択肢を初期化する
        /// </summary>
        /// <param name="mode">解像度の指定方法</param>
        /// <param name="displayName">表示名</param>
        public ResolutionModeOption(ResolutionMode mode, string displayName)
        {
            Mode = mode;
            DisplayName = displayName;
        }
    }
}
