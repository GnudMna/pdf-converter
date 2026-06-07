namespace PdfConverter.Models
{
    /// <summary>
    /// 出力画像ファイルの形式を表す列挙型
    /// </summary>
    public enum OutputImageFormat
    {
        /// <summary>PNG形式(透明度をサポート)</summary>
        Png,

        /// <summary>JPEG形式(透明度非対応)</summary>
        Jpeg,

        /// <summary>BMP形式</summary>
        Bmp,
    }
}
