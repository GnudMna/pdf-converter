using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PdfConverter.Tests.Helpers
{
    /// <summary>
    /// WPF ビットマップ生成のテスト用ヘルパー
    /// </summary>
    internal static class BitmapTestHelper
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        public static BitmapSource CreateBitmap(int width = 8, int height = 8)
        {
            var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
