using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfConverter.Models;

namespace PdfConverter.Services
{
    /// <summary>
    /// ビットマップの透明度処理・エンコードに関する共通処理
    /// </summary>
    public static class ImageBitmapHelper
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 透明度設定に応じてビットマップを加工する
        /// </summary>
        /// <param name="source">元画像</param>
        /// <param name="preserveTransparency"><c>true</c>の場合は透明度を保持する</param>
        /// <returns>加工後のビットマップ</returns>
        public static BitmapSource ApplyTransparency(BitmapSource source, bool preserveTransparency)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (preserveTransparency)
            {
                return source;
            }

            return FlattenOntoWhiteBackground(source);
        }

        /// <summary>
        /// 指定形式でビットマップをファイルに保存する
        /// </summary>
        /// <param name="source">保存する画像</param>
        /// <param name="outputPath">出力ファイルの絶対パス</param>
        /// <param name="format">出力形式</param>
        /// <exception cref="ArgumentNullException">sourceがnullの場合</exception>
        /// <exception cref="ArgumentException">outputPathが空の場合</exception>
        public static void SaveToFile(BitmapSource source, string outputPath, OutputImageFormat format)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("出力パスが指定されていません。", nameof(outputPath));
            }

            BitmapEncoder encoder = CreateEncoder(format);
            encoder.Frames.Add(BitmapFrame.Create(source));

            using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            {
                encoder.Save(fs);
            }
        }

        /// <summary>
        /// 出力形式に対応するファイル拡張子を返す(先頭のドットを含む)
        /// </summary>
        /// <param name="format">出力形式</param>
        /// <returns>ファイル拡張子(先頭のドットを含む)</returns>
        /// <exception cref="ArgumentOutOfRangeException">未対応の出力形式の場合</exception>
        public static string GetFileExtension(OutputImageFormat format)
        {
            switch (format)
            {
                case OutputImageFormat.Png:
                    return ".png";
                case OutputImageFormat.Jpeg:
                    return ".jpg";
                case OutputImageFormat.Bmp:
                    return ".bmp";
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, "未対応の出力形式です。");
            }
        }

        /// <summary>
        /// 指定形式が透明度をサポートするかどうかを返す
        /// </summary>
        /// <param name="format">出力形式</param>
        /// <returns>透明度をサポートするかどうか</returns>
        /// <exception cref="ArgumentOutOfRangeException">未対応の出力形式の場合</exception>
        public static bool SupportsTransparency(OutputImageFormat format)
        {
            return format != OutputImageFormat.Jpeg;
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// 透明チャンネルを持つ画像を白背景に合成する
        /// </summary>
        /// <param name="source">透明チャンネルを持つ画像</param>
        /// <returns>白背景に合成されたビットマップ</returns>
        private static BitmapSource FlattenOntoWhiteBackground(BitmapSource source)
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

        /// <summary>
        /// 出力形式に対応するビットマップエンコーダを返す
        /// </summary>
        /// <param name="format">出力形式</param>
        /// <returns>対応するビットマップエンコーダ</returns>
        /// <exception cref="ArgumentOutOfRangeException">未対応の出力形式の場合</exception>
        private static BitmapEncoder CreateEncoder(OutputImageFormat format)
        {
            switch (format)
            {
                case OutputImageFormat.Png:
                    return new PngBitmapEncoder();
                case OutputImageFormat.Jpeg:
                    return new JpegBitmapEncoder { QualityLevel = 90 };
                case OutputImageFormat.Bmp:
                    return new BmpBitmapEncoder();
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, "未対応の出力形式です。");
            }
        }
    }
}
