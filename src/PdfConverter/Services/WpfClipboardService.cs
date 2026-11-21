using System.IO;
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
        /*                                    定数                                    */
        /********************************************************************************/
        /// <summary>
        /// アルファ付き PNG を載せるクリップボード形式名
        /// </summary>
        internal const string PngFormat = "PNG";


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        public void CopyImage(BitmapSource image, bool preserveTransparency)
        {
            var data = new DataObject();

            if (preserveTransparency)
            {
                // Clipboard.SetImage はアルファを保持せず、透明部分が黒く貼り付けられることがある。
                // 透明度を扱える貼り付け先向けに PNG 形式を先頭に載せる
                data.SetData(PngFormat, EncodePng(image));
            }

            // PNG を読めない貼り付け先向けの標準ビットマップ。透明部分が黒にならないよう白背景に合成する
            data.SetImage(ImageBitmapHelper.ApplyTransparency(image, preserveTransparency: false));

            Clipboard.Clear();
            Clipboard.SetDataObject(data, copy: true);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// ビットマップをアルファ付き PNG のストリームにエンコードする
        /// </summary>
        /// <param name="image">エンコードする画像</param>
        /// <returns>先頭位置に戻した PNG のストリーム</returns>
        private static MemoryStream EncodePng(BitmapSource image)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));

            var stream = new MemoryStream();
            encoder.Save(stream);
            stream.Position = 0;
            return stream;
        }
    }
}
