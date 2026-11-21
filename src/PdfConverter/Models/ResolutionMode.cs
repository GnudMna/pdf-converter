namespace PdfConverter.Models
{
    /// <summary>
    /// PDFページを画像に変換する際の解像度指定方法を表す列挙型
    /// </summary>
    public enum ResolutionMode
    {
        /// <summary>PDFページの元のピクセルサイズをそのまま使用する</summary>
        Default,

        /// <summary>
        /// 出力幅(ピクセル)を指定する<br/>
        /// 高さはアスペクト比を維持して自動計算される
        /// </summary>
        Width,

        /// <summary>
        /// 出力高さ(ピクセル)を指定する<br/>
        /// 幅はアスペクト比を維持して自動計算される
        /// </summary>
        Height,

        /// <summary>
        /// 出力DPIを指定する<br/>
        /// 元画像のDPIに対するスケール比で拡縮される
        /// </summary>
        Dpi,
    }
}
