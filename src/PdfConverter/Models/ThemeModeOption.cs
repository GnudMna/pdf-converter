namespace PdfConverter.Models
{
    /// <summary>
    /// テーマ選択 ComboBox 用の表示項目
    /// </summary>
    public sealed class ThemeModeOption
    {
        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>テーマ種別</summary>
        public ThemeMode Mode { get; }

        /// <summary>UI 表示名</summary>
        public string DisplayName { get; }


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 指定したテーマ種別と表示名で選択肢を初期化する
        /// </summary>
        /// <param name="mode">テーマ種別</param>
        /// <param name="displayName">表示名</param>
        public ThemeModeOption(ThemeMode mode, string displayName)
        {
            Mode = mode;
            DisplayName = displayName;
        }
    }
}
