namespace PdfConverter.Models
{
    /// <summary>
    /// 解像度モードのComboBox表示用モデル
    /// </summary>
    public class ResolutionModeOption
    {
        /// <summary>解像度の指定方法</summary>
        public ResolutionMode Mode { get; }

        /// <summary>UIに表示するラベル</summary>
        public string DisplayName { get; }

        /// <summary>
        /// 指定したモードと表示名で選択肢を初期化する
        /// </summary>
        public ResolutionModeOption(ResolutionMode mode, string displayName)
        {
            Mode = mode;
            DisplayName = displayName;
        }
    }
}
