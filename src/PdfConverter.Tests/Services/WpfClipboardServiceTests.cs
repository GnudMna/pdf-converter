using System.Windows;
using System.Windows.Media.Imaging;
using PdfConverter.Services;
using PdfConverter.Tests.Helpers;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="WpfClipboardService"/> の画像コピー処理を検証する
    /// </summary>
    public class WpfClipboardServiceTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 画像をクリップボードへコピーできることを検証する
        /// </summary>
        [Fact]
        public void CopyImage_PlacesImageOnClipboard()
        {
            var service = new WpfClipboardService();
            BitmapSource image = null;

            StaTestHelper.Run(() =>
            {
                image = BitmapTestHelper.CreateBitmap();
                service.CopyImage(image, preserveTransparency: false);
                Assert.True(Clipboard.ContainsImage());
            });
        }

        /// <summary>
        /// 透過保持を有効にしてもクリップボードへ画像が設定されることを検証する
        /// </summary>
        [Fact]
        public void CopyImage_WithTransparency_PlacesImageOnClipboard()
        {
            var service = new WpfClipboardService();
            BitmapSource image = null;

            StaTestHelper.Run(() =>
            {
                image = BitmapTestHelper.CreateBitmap();
                service.CopyImage(image, preserveTransparency: true);
                Assert.True(Clipboard.ContainsImage());
            });
        }
    }
}
