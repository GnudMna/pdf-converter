using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PdfConverter.Services;

namespace PdfConverter.ViewModels.Coordinators
{
    /// <summary>
    /// PDF 読み込み・プレビュー生成・ページナビゲーションを担当する
    /// </summary>
    internal sealed class PdfPreviewCoordinator
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        private readonly IPdfConversionService _pdfService;
        private readonly IDialogService _dialogService;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 指定したサービスを使用してPDF読み込み・プレビュー生成・ページナビゲーションを担当する
        /// </summary>
        /// <param name="pdfService">PDF変換サービス</param>
        /// <param name="dialogService">ダイアログサービス</param>
        public PdfPreviewCoordinator(IPdfConversionService pdfService, IDialogService dialogService)
        {
            _pdfService = pdfService;
            _dialogService = dialogService;
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// <see cref="IMainViewModelHost.FilePath"/>のPDFを検証して読み込み、ページ数取得とプレビュー生成を開始する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <param name="forceReload">強制的に再読み込みするかどうか</param>
        public void LoadFromPath(IMainViewModelHost host, bool forceReload = false)
        {
            if (host.IsBusy)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(host.FilePath))
            {
                return;
            }

            if (!File.Exists(host.FilePath))
            {
                host.PageCount = 0;
                host.PreviewImage = null;
                host.LoadedFilePath = null;
                host.StatusMessage = "ファイルが見つかりません。パスを確認してください。";
                return;
            }

            if (!Path.GetExtension(host.FilePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                host.StatusMessage = "PDFファイル (.pdf) を指定してください。";
                return;
            }

            if (!forceReload && string.Equals(host.FilePath, host.LoadedFilePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            host.LoadedFilePath = host.FilePath;
            host.StatusMessage = "ファイルを読み込み中...";
            _ = LoadPageCountAndConvertAsync(host);
        }

        /// <summary>
        /// PDFが読み込み済みのとき、現在の解像度設定でプレビューを再生成する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <returns>非同期操作のタスク</returns>
        public Task RefreshIfLoadedAsync(IMainViewModelHost host)
        {
            if (string.IsNullOrEmpty(host.FilePath) || host.PageCount <= 0 || host.IsBusy)
            {
                return Task.CompletedTask;
            }

            return ConvertAsync(host);
        }

        /// <summary>
        /// 指定されたページに移動し、プレビューを更新する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <returns>非同期操作のタスク</returns>
        public async Task GoToPageAsync(IMainViewModelHost host)
        {
            if (host.PageCount <= 0)
            {
                host.StatusMessage = "ページ数が不明です。PDFを読み込んでください。";
                return;
            }

            await ConvertAsync(host, showResolutionDialog: true);
        }

        /// <summary>
        /// 前のページに移動し、プレビューを更新する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <returns>非同期操作のタスク</returns>
        public async Task GoToPreviousPageAsync(IMainViewModelHost host)
        {
            if (host.CurrentPreviewPage <= 1)
            {
                return;
            }

            host.PageNumber = (host.CurrentPreviewPage - 1).ToString();
            await ConvertAsync(host, showResolutionDialog: true);
        }

        /// <summary>
        /// 次のページに移動し、プレビューを更新する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <returns>非同期操作のタスク</returns>
        public async Task GoToNextPageAsync(IMainViewModelHost host)
        {
            if (host.CurrentPreviewPage >= host.PageCount)
            {
                return;
            }

            host.PageNumber = (host.CurrentPreviewPage + 1).ToString();
            await ConvertAsync(host, showResolutionDialog: true);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// PDFのページ数を取得し、プレビューを生成する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <returns>非同期操作のタスク</returns>
        private async Task LoadPageCountAndConvertAsync(IMainViewModelHost host)
        {
            if (string.IsNullOrEmpty(host.FilePath))
            {
                return;
            }

            host.ProgressValue = 0;
            host.PrepareCancellation();
            host.IsBusy = true;
            CancellationToken cancellationToken = host.GetCancellationToken();
            string loadingPath = host.FilePath;

            try
            {
                try
                {
                    host.PageCount = await _pdfService.GetPdfPageCountAsync(loadingPath, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (Exception ex) when (CancellationExceptionHelper.IsOrContainsCancellation(ex))
                {
                    return;
                }
                catch (Exception ex)
                {
                    host.PageCount = 0;
                    host.PreviewImage = null;
                    host.LoadedFilePath = null;
                    host.StatusMessage = $"PDFの読み込みに失敗しました: {ex.Message}";
                    return;
                }

                if (!string.Equals(host.FilePath, loadingPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (host.PageCount > 0 && (!int.TryParse(host.PageNumber, out int current) || current < 1 || current > host.PageCount))
                {
                    host.PageNumber = "1";
                }

                await ConvertAsync(host, showResolutionDialog: true, manageBusyState: false);
            }
            finally
            {
                host.IsBusy = false;
                host.DisposeCancellation();
            }
        }

        /// <summary>
        /// PDFのページを画像に変換し、プレビューを更新する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <param name="showResolutionDialog">解像度ダイアログを表示するかどうか</param>
        /// <param name="manageBusyState">処理中フラグとキャンセルソースのライフサイクルをこのメソッドで管理するかどうか</param>
        /// <returns>非同期操作のタスク</returns>
        private async Task ConvertAsync(IMainViewModelHost host, bool showResolutionDialog = false, bool manageBusyState = true)
        {
            if (string.IsNullOrEmpty(host.FilePath))
            {
                return;
            }

            host.ProgressValue = 0;
            host.PrepareCancellation();
            CancellationToken cancellationToken = host.GetCancellationToken();
            if (manageBusyState)
            {
                host.IsBusy = true;
            }

            host.StatusMessage = "プレビュー生成中...";

            try
            {
                if (!int.TryParse(host.PageNumber, out int pageNumber) || pageNumber < 1)
                {
                    _dialogService.ShowMessage("有効なページ番号を入力してください。");
                    host.StatusMessage = "有効なページ番号を入力してください。";
                    return;
                }

                if (!CoordinatorHelpers.TryGetResolutionValue(host, _dialogService, out double val, showResolutionDialog))
                {
                    return;
                }

                int pageIndex = pageNumber - 1;
                host.PreviewImage = await _pdfService.ConvertPdfPageToImageAsync(
                    host.FilePath,
                    pageIndex,
                    host.ResolutionMode,
                    val,
                    host.PreserveTransparency,
                    cancellationToken);
                host.StatusMessage = "プレビューを更新しました。";
            }
            catch (Exception ex) when (CancellationExceptionHelper.IsOrContainsCancellation(ex))
            {
                host.StatusMessage = "プレビュー変換をキャンセルしました。";
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"PDF変換エラー: {ex.Message}");
                host.StatusMessage = "プレビューの生成中にエラーが発生しました。";
            }
            finally
            {
                if (manageBusyState)
                {
                    host.IsBusy = false;
                    host.DisposeCancellation();
                }
            }
        }
    }
}
