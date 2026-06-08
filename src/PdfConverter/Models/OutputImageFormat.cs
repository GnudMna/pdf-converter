namespace PdfConverter.Models
{
    /// <summary>
    /// 出力画像ファイルの形式を表す列挙型
    /// </summary>
    public enum OutputImageFormat
    {
        /// <summary>PNG 形式 (透明度をサポート)</summary>
        Png,

        /// <summary>JPEG 形式 (透明度非対応)</summary>
        Jpeg,

        /// <summary>BMP 形式</summary>
        Bmp,
    }
}
