using System.Threading;
using System.Threading.Tasks;

namespace PdfConverter.Services
{
    /// <summary>
    /// WordファイルをPDFに変換するサービスのコントラクト
    /// </summary>
    public interface IWordToPdfConversionService
    {
        /********************************************************************************/
        /*                                 抽象メソッド                                 */
        /********************************************************************************/
        /// <summary>
        /// Wordファイルを一時PDFファイルへ変換する
        /// </summary>
        /// <param name="wordFilePath">Wordファイルの絶対パス</param>
        /// <param name="cancellationToken">処理をキャンセルするためのトークン</param>
        /// <returns>生成されたPDFファイルの絶対パス</returns>
        Task<string> ConvertToPdfAsync(string wordFilePath, CancellationToken cancellationToken = default);
    }
}
