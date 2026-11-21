using System.Threading.Tasks;
using System.Windows.Media.Imaging;

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
        /// <see cref="IPreviewCoordinatorHost.FilePath"/> の PDF / Word ファイルを検証して読み込み、ページ数取得とプレビュー生成を開始する
        /// </summary>
        /// <param name="host">プレビュー操作ホスト</param>
        /// <param name="forceReload">強制的に再読み込みするかどうか</param>
        void LoadFromPath(IPreviewCoordinatorHost host, bool forceReload = false);

        /// <summary>
        /// PDF が読み込み済みのとき、現在の解像度設定でプレビューを再生成する
        /// </summary>
        /// <param name="host">プレビュー操作ホスト</param>
        /// <returns>非同期操作のタスク</returns>
        Task RefreshIfLoadedAsync(IPreviewCoordinatorHost host);

        /// <summary>
        /// 読み込み済み PDF のプレビュー再生成をスケジュールする
        /// </summary>
        /// <param name="host">プレビュー操作ホスト</param>
        void RequestRefreshIfLoaded(IPreviewCoordinatorHost host);

        /// <summary>
        /// 保存中・出力解像度の画像の生成中に保留したプレビュー再生成を、処理の完了後に 1 回だけ実行する
        /// </summary>
        /// <param name="host">プレビュー操作ホスト</param>
        void ResumeDeferredRefresh(IPreviewCoordinatorHost host);

        /// <summary>
        /// 指定されたページに移動し、プレビューを更新する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <returns>非同期操作のタスク</returns>
        Task GoToPageAsync(IPreviewCoordinatorHost host);

        /// <summary>
        /// 前のページに移動し、プレビューを更新する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <returns>非同期操作のタスク</returns>
        Task GoToPreviousPageAsync(IPreviewCoordinatorHost host);

        /// <summary>
        /// 次のページに移動し、プレビューを更新する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <returns>非同期操作のタスク</returns>
        Task GoToNextPageAsync(IPreviewCoordinatorHost host);

        /// <summary>
        /// プレビュー画像を表示する領域のサイズ変更を通知し、表示中のプレビューが粗くなる場合は少し待ってから描画し直す
        /// </summary>
        /// <param name="host">プレビュー操作ホスト</param>
        void NotifyPreviewViewportChanged(IPreviewCoordinatorHost host);

        /// <summary>
        /// 表示中のプレビューと同じページ・設定の画像を出力解像度で取得する
        /// </summary>
        /// <remarks>
        /// プレビューが表示サイズに縮小されている場合のみ出力解像度で描画し直し、それ以外は表示中の画像をそのまま返す
        /// </remarks>
        /// <param name="host">プレビュー操作ホスト</param>
        /// <returns>出力解像度の画像。プレビューがない・キャンセル・失敗の場合は <c>null</c> (ステータスは設定済み)</returns>
        Task<BitmapSource?> GetOutputResolutionPreviewImageAsync(IPreviewCoordinatorHost host);
    }
}
