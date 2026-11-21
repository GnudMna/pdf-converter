namespace PdfConverter.Infrastructure
{
    /// <summary>
    /// アセットダウンロード用の埋め込みキー (値はビルド時に <c>UpdateReleaseKey.g.cs</c> で生成)
    /// </summary>
    internal static partial class UpdateReleaseKey
    {
        /// <summary>
        /// キーが埋め込まれているか
        /// </summary>
        internal static bool IsConfigured => !string.IsNullOrEmpty(Value);
    }
}
