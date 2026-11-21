namespace PdfConverter.Models
{
    /// <summary>
    /// 出力画像ファイルの形式を表す列挙型
    /// </summary>
    public enum OutputImageFormat
    {
        /// <summary>JPEG形式(透明度非対応)</summary>
        Jpeg,

        /// <summary>PNG形式(透明度をサポート)</summary>
        Png,

        /// <summary>BMP形式</summary>
        Bmp,
    }
}
