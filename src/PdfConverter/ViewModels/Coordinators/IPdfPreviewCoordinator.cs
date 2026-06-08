using System.Threading.Tasks;

namespace PdfConverter.ViewModels.Coordinators
{
    /// <summary>
    /// PDF 読み込み・プレビュー生成・ページナビゲーションを担当する
    /// </summary>
    public interface IPdfPreviewCoordinator
    {
        /********************************************************************************/
        /*                                 抽象メソッド                                 */
        /********************************************************************************/
        /// <summary>
        /// <see cref="IMainViewModelHost.FilePath"/> の PDF を検証して読み込み、ページ数取得とプレビュー生成を開始する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <param name="forceReload">強制的に再読み込みするかどうか</param>
        void LoadFromPath(IMainViewModelHost host, bool forceReload = false);

        /// <summary>
        /// PDF が読み込み済みのとき、現在の解像度設定でプレビューを再生成する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <returns>非同期操作のタスク</returns>
        Task RefreshIfLoadedAsync(IMainViewModelHost host);

        /// <summary>
        /// 読み込み済み PDF のプレビュー再生成をスケジュールする
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        void RequestRefreshIfLoaded(IMainViewModelHost host);

        /// <summary>
        /// 指定されたページに移動し、プレビューを更新する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <returns>非同期操作のタスク</returns>
        Task GoToPageAsync(IMainViewModelHost host);

        /// <summary>
        /// 前のページに移動し、プレビューを更新する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <returns>非同期操作のタスク</returns>
        Task GoToPreviousPageAsync(IMainViewModelHost host);

        /// <summary>
        /// 次のページに移動し、プレビューを更新する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <returns>非同期操作のタスク</returns>
        Task GoToNextPageAsync(IMainViewModelHost host);
    }
}
