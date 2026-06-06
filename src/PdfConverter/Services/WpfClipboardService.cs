using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PdfConverter.Services
{
    /// <summary>
    /// WPFクリップボードAPIを使用する<see cref="IClipboardService"/>の実装
    /// </summary>
    public class WpfClipboardService : IClipboardService
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        public void CopyImage(BitmapSource image)
        {
            var flattened = CreateFlattenedImage(image);
            Clipboard.Clear();
            Clipboard.SetImage(flattened);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// 透明チャンネルを持つ画像を白背景に合成し、クリップボード互換性を高める
        /// </summary>
        private static BitmapSource CreateFlattenedImage(BitmapSource source)
        {
            var width = source.PixelWidth;
            var height = source.PixelHeight;
            var dpiX = source.DpiX;
            var dpiY = source.DpiY;

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
                dc.DrawImage(source, new Rect(0, 0, width, height));
            }

            var bitmap = new RenderTargetBitmap(width, height, dpiX, dpiY, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
