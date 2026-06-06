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
        /// <param name="preserveTransparency"><c>true</c>の場合は透明度を保持する</param>
        void CopyImage(BitmapSource image, bool preserveTransparency);
    }
}
