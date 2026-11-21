namespace PdfConverter.Models
{
    /// <summary>
    /// Word → PDF 変換に使用するエンジン
    /// </summary>
    public enum WordToPdfBackend
    {
        /********************************************************************************/
        /*                                    列挙値                                    */
        /********************************************************************************/
        /// <summary>Microsoft Word (COM)</summary>
        MicrosoftWord,

        /// <summary>LibreOffice (headless)</summary>
        LibreOffice,
    }
}
