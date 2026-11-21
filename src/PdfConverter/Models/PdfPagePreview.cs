using System.Windows.Media.Imaging;

namespace PdfConverter.Models
{
    /// <summary>
    /// 表示サイズに合わせて描画した PDF ページのプレビューを保持する不変データオブジェクト
    /// </summary>
    public sealed class PdfPagePreview
    {
        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>プレビュー画像</summary>
        public BitmapSource Image { get; }

        /// <summary>出力解像度で描画した場合の幅 (px)</summary>
        public int OutputPixelWidth { get; }

        /// <summary>出力解像度で描画した場合の高さ (px)</summary>
        public int OutputPixelHeight { get; }

        /// <summary>プレビュー画像が出力解像度より縮小されているかどうか</summary>
        public bool IsDownscaled => Image.PixelWidth < OutputPixelWidth || Image.PixelHeight < OutputPixelHeight;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// プレビューを初期化する
        /// </summary>
        /// <param name="image">プレビュー画像</param>
        /// <param name="outputPixelWidth">出力解像度で描画した場合の幅 (px)</param>
        /// <param name="outputPixelHeight">出力解像度で描画した場合の高さ (px)</param>
        public PdfPagePreview(BitmapSource image, int outputPixelWidth, int outputPixelHeight)
        {
            Image = image;
            OutputPixelWidth = outputPixelWidth;
            OutputPixelHeight = outputPixelHeight;
        }
    }
}
