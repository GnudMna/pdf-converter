using System.Threading;
using System.Threading.Tasks;

namespace PdfConverter.Services
{
    /// <summary>
    /// GitHub Release からの更新確認・適用
    /// </summary>
    public interface IUpdateService
    {
        /********************************************************************************/
        /*                                 抽象メソッド                                 */
        /********************************************************************************/
        /// <summary>
        /// 最新リリースを照会し、更新の有無を判定する
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>確認結果</returns>
        Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 更新アセットをダウンロード・検証し、インストーラー (ポータブル版は展開した新しい版の更新処理) を起動する
        /// </summary>
        /// <param name="update">確認済みの更新情報</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>適用結果</returns>
        Task<UpdateApplyResult> ApplyUpdateAsync(
            AvailableUpdate? update,
            CancellationToken cancellationToken = default);
    }
}
