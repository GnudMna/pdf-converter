namespace PdfConverter.Models
{
    /// <summary>
    /// ステータスバー表示の種類
    /// </summary>
    public enum StatusKind
    {
        /// <summary>通常の案内</summary>
        Info,

        /// <summary>処理中</summary>
        Progress,

        /// <summary>成功</summary>
        Success,

        /// <summary>警告</summary>
        Warning,

        /// <summary>エラー</summary>
        Error,
    }
}
