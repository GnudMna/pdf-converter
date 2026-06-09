namespace PdfConverter.Models
{
    /// <summary>
    /// Word → PDF 変換時の PDF 形式
    /// </summary>
    public enum WordToPdfPdfFormat
    {
        /********************************************************************************/
        /*                                    列挙値                                    */
        /********************************************************************************/
        /// <summary>標準 PDF (PDF 1.4)</summary>
        Standard,

        /// <summary>PDF/A-1 (アーカイブ用)</summary>
        PdfA,
    }
}
