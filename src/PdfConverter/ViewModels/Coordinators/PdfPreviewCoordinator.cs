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
    internal sealed class PdfPreviewCoordinator : IPdfPreviewCoordinator
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        private readonly IPdfConversionService _pdfService;
        private readonly IDialogService _dialogService;
        private readonly object _previewTaskLock = new object();
        private long _previewOperationGeneration;
        private Task _activePreviewTask = Task.CompletedTask;


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
            if (host.IsSaving)
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
            long operationGeneration = BeginPreviewOperation(host);
            TrackPreviewTask(LoadPageCountAndConvertAsync(host, operationGeneration));
        }

        /// <summary>
        /// PDFが読み込み済みのとき、現在の解像度設定でプレビューを再生成する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <returns>非同期操作のタスク</returns>
        public Task RefreshIfLoadedAsync(IMainViewModelHost host)
        {
            if (string.IsNullOrEmpty(host.FilePath) || host.PageCount <= 0 || host.IsSaving)
            {
                return Task.CompletedTask;
            }

            long operationGeneration = BeginPreviewOperation(host);
            return ConvertAsync(host, operationGeneration, showResolutionDialog: false, manageBusyState: true);
        }

        /// <summary>
        /// 読み込み済み PDF のプレビュー再生成をスケジュールする<br/>
        /// プロパティセッターなど UI スレッドから呼び出す用途向け
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        public void RequestRefreshIfLoaded(IMainViewModelHost host)
        {
            TrackPreviewTask(RefreshIfLoadedAsync(host));
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

            long operationGeneration = BeginPreviewOperation(host);
            await ConvertAsync(host, operationGeneration, showResolutionDialog: true, manageBusyState: true);
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
            long operationGeneration = BeginPreviewOperation(host);
            await ConvertAsync(host, operationGeneration, showResolutionDialog: true, manageBusyState: true);
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
            long operationGeneration = BeginPreviewOperation(host);
            await ConvertAsync(host, operationGeneration, showResolutionDialog: true, manageBusyState: true);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// 新しいプレビュー操作を開始し、進行中の操作をキャンセルする
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <returns>今回の操作世代番号</returns>
        private long BeginPreviewOperation(IMainViewModelHost host)
        {
            host.PrepareCancellation();
            return Interlocked.Increment(ref _previewOperationGeneration);
        }

        /// <summary>
        /// 指定世代のプレビュー操作が最新かどうかを判定する
        /// </summary>
        /// <param name="operationGeneration">操作開始時に取得した世代番号</param>
        /// <returns>最新の操作であれば true</returns>
        private bool IsCurrentPreviewOperation(long operationGeneration)
        {
            return operationGeneration == Volatile.Read(ref _previewOperationGeneration);
        }

        /// <summary>
        /// 追跡対象のプレビュータスクを登録し、未処理例外を観測する
        /// </summary>
        /// <param name="previewTask">プレビュー処理タスク</param>
        private void TrackPreviewTask(Task previewTask)
        {
            if (previewTask == null)
            {
                return;
            }

            lock (_previewTaskLock)
            {
                _activePreviewTask = previewTask;
            }

            if (previewTask.IsCompleted)
            {
                ObservePreviewTaskFault(previewTask);
                return;
            }

            previewTask.ContinueWith(
                ObservePreviewTaskFault,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        /// <summary>
        /// プレビュータスクの未処理例外を観測する
        /// </summary>
        private static void ObservePreviewTaskFault(Task completedTask)
        {
            if (completedTask.IsFaulted)
            {
                _ = completedTask.Exception;
            }
        }

        /// <summary>
        /// PDFのページ数を取得し、プレビューを生成する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <param name="operationGeneration">操作世代番号</param>
        /// <returns>非同期操作のタスク</returns>
        private async Task LoadPageCountAndConvertAsync(IMainViewModelHost host, long operationGeneration)
        {
            if (string.IsNullOrEmpty(host.FilePath))
            {
                return;
            }

            host.ProgressValue = 0;
            host.IsBusy = true;
            CancellationToken cancellationToken = host.GetCancellationToken();
            string loadingPath = host.FilePath;

            try
            {
                try
                {
                    int pageCount = await _pdfService.GetPdfPageCountAsync(loadingPath, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!IsCurrentPreviewOperation(operationGeneration))
                    {
                        return;
                    }

                    host.PageCount = pageCount;
                }
                catch (Exception ex) when (CancellationExceptionHelper.IsOrContainsCancellation(ex))
                {
                    return;
                }
                catch (Exception ex)
                {
                    if (!IsCurrentPreviewOperation(operationGeneration))
                    {
                        return;
                    }

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

                await ConvertAsync(host, operationGeneration, showResolutionDialog: true, manageBusyState: false);
            }
            finally
            {
                if (IsCurrentPreviewOperation(operationGeneration))
                {
                    host.IsBusy = false;
                    host.DisposeCancellation();
                }
            }
        }

        /// <summary>
        /// PDFのページを画像に変換し、プレビューを更新する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <param name="operationGeneration">操作世代番号</param>
        /// <param name="showResolutionDialog">解像度ダイアログを表示するかどうか</param>
        /// <param name="manageBusyState">処理中フラグとキャンセルソースのライフサイクルをこのメソッドで管理するかどうか</param>
        /// <returns>非同期操作のタスク</returns>
        private async Task ConvertAsync(
            IMainViewModelHost host,
            long operationGeneration,
            bool showResolutionDialog = false,
            bool manageBusyState = true)
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
                    if (!IsCurrentPreviewOperation(operationGeneration))
                    {
                        return;
                    }

                    _dialogService.ShowMessage("有効なページ番号を入力してください。");
                    host.StatusMessage = "有効なページ番号を入力してください。";
                    return;
                }

                if (!CoordinatorHelpers.TryGetResolutionValue(host, _dialogService, out double val, showResolutionDialog))
                {
                    return;
                }

                if (!IsCurrentPreviewOperation(operationGeneration))
                {
                    return;
                }

                int pageIndex = pageNumber - 1;
                var previewImage = await _pdfService.ConvertPdfPageToImageAsync(
                    host.FilePath,
                    pageIndex,
                    host.ResolutionMode,
                    val,
                    host.PreserveTransparency,
                    cancellationToken);

                if (!IsCurrentPreviewOperation(operationGeneration))
                {
                    return;
                }

                host.PreviewImage = previewImage;
                host.StatusMessage = "プレビューを更新しました。";
            }
            catch (Exception ex) when (CancellationExceptionHelper.IsOrContainsCancellation(ex))
            {
                if (IsCurrentPreviewOperation(operationGeneration))
                {
                    host.StatusMessage = "プレビュー変換をキャンセルしました。";
                }
            }
            catch (Exception ex)
            {
                if (!IsCurrentPreviewOperation(operationGeneration))
                {
                    return;
                }

                _dialogService.ShowMessage($"PDF変換エラー: {ex.Message}");
                host.StatusMessage = "プレビューの生成中にエラーが発生しました。";
            }
            finally
            {
                if (manageBusyState && IsCurrentPreviewOperation(operationGeneration))
                {
                    host.IsBusy = false;
                    host.DisposeCancellation();
                }
            }
        }
    }
}
