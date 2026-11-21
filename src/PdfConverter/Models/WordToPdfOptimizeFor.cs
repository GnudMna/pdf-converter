namespace PdfConverter.Models
{
    /// <summary>
    /// Word → PDF 変換時の出力最適化モード
    /// </summary>
    public enum WordToPdfOptimizeFor
    {
        /********************************************************************************/
        /*                                    列挙値                                    */
        /********************************************************************************/
        /// <summary>印刷向け (高品質)</summary>
        Print,

        /// <summary>オンライン向け (ファイルサイズ優先)</summary>
        Online,
    }
}
