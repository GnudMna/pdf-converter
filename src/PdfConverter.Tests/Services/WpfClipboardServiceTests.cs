using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
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

        /// <summary>
        /// 透過保持を有効にすると、アルファを保持した PNG 形式もクリップボードへ載ることを検証する
        /// </summary>
        [Fact]
        public void CopyImage_WithTransparency_PlacesAlphaPreservedPngOnClipboard()
        {
            var service = new WpfClipboardService();

            StaTestHelper.Run(() =>
            {
                // 全ピクセルが完全透明の画像
                var image = BitmapTestHelper.CreateBitmap();
                service.CopyImage(image, preserveTransparency: true);

                var png = ReadRawClipboardFormat(WpfClipboardService.PngFormat);
                Assert.NotNull(png);
                var decoder = new PngBitmapDecoder(new MemoryStream(png), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var converted = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
                var pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4];
                converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);

                for (var i = 3; i < pixels.Length; i += 4)
                {
                    Assert.Equal(0, pixels[i]);
                }
            });
        }

        /// <summary>
        /// 透過保持を無効にすると、PNG 形式は載らず標準ビットマップのみが載ることを検証する
        /// </summary>
        [Fact]
        public void CopyImage_WithoutTransparency_DoesNotPlacePngOnClipboard()
        {
            var service = new WpfClipboardService();

            StaTestHelper.Run(() =>
            {
                var image = BitmapTestHelper.CreateBitmap();
                service.CopyImage(image, preserveTransparency: false);

                Assert.Null(ReadRawClipboardFormat(WpfClipboardService.PngFormat));
                Assert.True(Clipboard.ContainsImage());
            });
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// 指定した名前のクリップボード形式の生バイト列を、他アプリと同じ Win32 API で読み取る
        /// </summary>
        /// <param name="formatName">クリップボード形式名</param>
        /// <returns>バイト列。形式が無い場合は <c>null</c></returns>
        private static byte[] ReadRawClipboardFormat(string formatName)
        {
            var format = RegisterClipboardFormat(formatName);
            if (!OpenClipboard(IntPtr.Zero))
            {
                throw new InvalidOperationException("クリップボードを開けませんでした。");
            }

            try
            {
                var handle = GetClipboardData(format);
                if (handle == IntPtr.Zero)
                {
                    return null;
                }

                var size = (int)GlobalSize(handle);
                var pointer = GlobalLock(handle);
                try
                {
                    var bytes = new byte[size];
                    Marshal.Copy(pointer, bytes, 0, size);
                    return bytes;
                }
                finally
                {
                    GlobalUnlock(handle);
                }
            }
            finally
            {
                CloseClipboard();
            }
        }


        /********************************************************************************/
        /*                               Win32 API 定義                                 */
        /********************************************************************************/
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegisterClipboardFormat(string lpszFormat);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("kernel32.dll")]
        private static extern UIntPtr GlobalSize(IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern bool GlobalUnlock(IntPtr hMem);
    }
}
