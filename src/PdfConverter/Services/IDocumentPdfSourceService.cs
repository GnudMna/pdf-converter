using System.Threading;
using System.Threading.Tasks;

namespace PdfConverter.Services
{
    /// <summary>
    /// 入力ドキュメントから PDF レンダリング用のパスを提供するサービスのコントラクト
    /// </summary>
    public interface IDocumentPdfSourceService
    {
        /********************************************************************************/
        /*                                 抽象メソッド                                 */
        /********************************************************************************/
        /// <summary>
        /// 指定したパスがサポート対象のドキュメントかどうかを判定する
        /// </summary>
        /// <param name="sourcePath">入力ファイルの絶対パス</param>
        /// <returns>true: サポート対象 / false: サポート対象ではない</returns>
        bool IsSupportedDocument(string sourcePath);

        /// <summary>
        /// PDF レンダリングに使用するパスを取得する
        /// </summary>
        /// <remarks>
        /// PDF 入力の場合は元ファイルを返し、Word 入力の場合は PDF へ変換した一時ファイルを返す
        /// </remarks>
        /// <param name="sourcePath">入力ファイルの絶対パス</param>
        /// <param name="cancellationToken">処理をキャンセルするためのトークン</param>
        /// <returns>PDF レンダリング用の絶対パス</returns>
        Task<string> GetPdfPathAsync(string sourcePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// 指定した入力ファイルに関連する一時 PDF を破棄する
        /// </summary>
        /// <param name="sourcePath">入力ファイルの絶対パス</param>
        void Invalidate(string sourcePath);
    }
}
