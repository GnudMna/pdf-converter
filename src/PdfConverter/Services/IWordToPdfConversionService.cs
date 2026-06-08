using System.Threading;
using System.Threading.Tasks;

namespace PdfConverter.Services
{
    /// <summary>
    /// Word ファイルを PDF に変換するサービスのコントラクト
    /// </summary>
    public interface IWordToPdfConversionService
    {
        /********************************************************************************/
        /*                                 抽象メソッド                                 */
        /********************************************************************************/
        /// <summary>
        /// Word ファイルを一時 PDF ファイルへ変換する
        /// </summary>
        /// <param name="wordFilePath">Word ファイルの絶対パス</param>
        /// <param name="cancellationToken">処理をキャンセルするためのトークン</param>
        /// <returns>生成された PDF ファイルの絶対パス</returns>
        Task<string> ConvertToPdfAsync(string wordFilePath, CancellationToken cancellationToken = default);
    }
}
