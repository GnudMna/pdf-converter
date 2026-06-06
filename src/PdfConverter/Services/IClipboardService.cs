using System.Windows.Media.Imaging;

namespace PdfConverter.Services
{
    /// <summary>
    /// クリップボード操作の抽象化
    /// </summary>
    public interface IClipboardService
    {
        /********************************************************************************/
        /*                                 抽象メソッド                                 */
        /********************************************************************************/
        /// <summary>
        /// ビットマップをクリップボードにコピーする
        /// </summary>
        /// <param name="image">コピーする画像</param>
        void CopyImage(BitmapSource image);
    }
}
