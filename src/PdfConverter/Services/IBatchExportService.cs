using System.Threading;
using System.Threading.Tasks;
using PdfConverter.Infrastructure;

namespace PdfConverter.Services
{
    /// <summary>
    /// バッチ / ヘッドレスモードでの変換処理
    /// </summary>
    public interface IBatchExportService
    {
        /********************************************************************************/
        /*                                 抽象メソッド                                 */
        /********************************************************************************/
        /// <summary>
        /// コマンドライン引数に基づいて変換を実行する
        /// </summary>
        /// <param name="options">バッチモードのオプション</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>プロセス終了コード</returns>
        Task<int> RunAsync(BatchCliOptions options, CancellationToken cancellationToken = default);
    }
}
