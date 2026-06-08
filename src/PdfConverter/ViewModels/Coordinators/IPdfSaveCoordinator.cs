using System.Threading.Tasks;

namespace PdfConverter.ViewModels.Coordinators
{
    /// <summary>
    /// PDF ページの一括保存と上書き確認を担当する
    /// </summary>
    public interface IPdfSaveCoordinator
    {
        /********************************************************************************/
        /*                                 抽象メソッド                                 */
        /********************************************************************************/
        /// <summary>
        /// 指定ページを画像ファイルとして保存する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <returns>非同期操作のタスク</returns>
        Task SaveAsync(IMainViewModelHost host);

        /// <summary>
        /// Word から変換した PDF をファイルとして保存する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <returns>非同期操作のタスク</returns>
        Task SavePdfAsync(IMainViewModelHost host);
    }
}
