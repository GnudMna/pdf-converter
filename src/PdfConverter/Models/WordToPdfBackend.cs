namespace PdfConverter.Models
{
    /// <summary>
    /// Word → PDF変換に使用するエンジン
    /// </summary>
    public enum WordToPdfBackend
    {
        /// <summary>Microsoft Word (COM)</summary>
        MicrosoftWord,

        /// <summary>LibreOffice (headless)</summary>
        LibreOffice,
    }
}
