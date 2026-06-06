namespace PdfConverter.Models
{
    /// <summary>
    /// テーマ選択ComboBox用の表示項目
    /// </summary>
    public sealed class ThemeModeOption
    {
        public ThemeModeOption(ThemeMode mode, string displayName)
        {
            Mode = mode;
            DisplayName = displayName;
        }

        /// <summary>テーマ種別</summary>
        public ThemeMode Mode { get; }

        /// <summary>UI表示名</summary>
        public string DisplayName { get; }
    }
}
