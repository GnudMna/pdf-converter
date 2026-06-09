using System.Windows;
using System.Windows.Media.Imaging;

namespace PdfConverter.Services
{
    /// <summary>
    /// WPF クリップボード API を使用する <see cref="IClipboardService"/> の実装
    /// </summary>
    public class WpfClipboardService : IClipboardService
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        public void CopyImage(BitmapSource image, bool preserveTransparency)
        {
            var processed = ImageBitmapHelper.ApplyTransparency(image, preserveTransparency);
            Clipboard.Clear();
            Clipboard.SetImage(processed);
        }
    }
}
