using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PdfConverter.Infrastructure;
using PdfConverter.Models;
using PdfConverter.Services;

namespace PdfConverter.ViewModels.Coordinators
{
    /// <summary>
    /// PDF読み込み・プレビュー生成・ページナビゲーションを担当する
    /// </summary>
    internal sealed class PdfPreviewCoordinator : IPdfPreviewCoordinator
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>PDF変換サービス</summary>
        private readonly IPdfConversionService _pdfService;

        /// <summary>入力ドキュメントからPDFソースを提供するサービス</summary>
        private readonly IDocumentPdfSourceService _documentPdfSourceService;

        /// <summary>プレビュータスクの排他制御用オブジェクト</summary>
        private readonly object _previewTaskLock = new object();

        /// <summary>プレビュー操作世代番号</summary>
        private long _previewOperationGeneration;

        /// <summary>アクティブなプレビュータスク</summary>
        private Task _activePreviewTask = Task.CompletedTask;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 指定したサービスを使用してPDF読み込み・プレビュー生成・ページナビゲーションを担当する
        /// </summary>
        /// <param name="pdfService">PDF変換サービス</param>
        /// <param name="documentPdfSourceService">入力ドキュメントからPDFソースを提供するサービス</param>
        public PdfPreviewCoordinator(
            IPdfConversionService pdfService,
            IDocumentPdfSourceService documentPdfSourceService)
        {
            _pdfService = pdfService;
            _documentPdfSourceService = documentPdfSourceService;
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
                host.SetStatus("ファイルが見つかりません。パスを確認してください。", StatusKind.Error);
                return;
            }

            if (!_documentPdfSourceService.IsSupportedDocument(host.FilePath))
            {
                host.SetStatus("PDF (.pdf) または Word (.doc / .docx) ファイルを指定してください。", StatusKind.Warning);
                return;
            }

            if (!forceReload && string.Equals(host.FilePath, host.LoadedFilePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!string.IsNullOrEmpty(host.LoadedFilePath)
                && !string.Equals(host.FilePath, host.LoadedFilePath, StringComparison.OrdinalIgnoreCase))
            {
                _documentPdfSourceService.Invalidate(host.LoadedFilePath);
            }

            host.LoadedFilePath = host.FilePath;
            host.SetStatus("ファイルを読み込み中...", StatusKind.Progress);
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
            return ConvertAsync(host, operationGeneration, showFieldValidation: false, manageBusyState: true);
        }

        /// <summary>
        /// 読み込み済みPDFのプレビュー再生成をスケジュールする
        /// </summary>
        /// <remarks>プロパティセッターなどUIスレッドから呼び出す用途向け</remarks>
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
                host.SetStatus("ページ数が不明です。PDFを読み込んでください。", StatusKind.Warning);
                return;
            }

            long operationGeneration = BeginPreviewOperation(host);
            await ConvertAsync(host, operationGeneration, showFieldValidation: true, manageBusyState: true);
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
            await ConvertAsync(host, operationGeneration, showFieldValidation: true, manageBusyState: true);
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
            await ConvertAsync(host, operationGeneration, showFieldValidation: true, manageBusyState: true);
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
        /// <returns>true: 最新の操作である / false: 最新の操作ではない</returns>
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
        /// <param name="completedTask">完了したプレビュータスク</param>
        private static void ObservePreviewTaskFault(Task completedTask)
        {
            if (completedTask.IsFaulted && completedTask.Exception != null)
            {
                GlobalExceptionHandler.Report(completedTask.Exception, "PreviewTask");
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
                    string pdfPath = await ResolvePdfPathAsync(host, loadingPath, operationGeneration, cancellationToken);
                    if (pdfPath == null)
                    {
                        return;
                    }

                    int pageCount = await _pdfService.GetPdfPageCountAsync(pdfPath, cancellationToken);
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
                    host.SetStatus($"PDFの読み込みに失敗しました: {ex.Message}", StatusKind.Error);
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

                await ConvertAsync(host, operationGeneration, showFieldValidation: true, manageBusyState: false);
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
        /// <param name="showFieldValidation">入力欄の検証メッセージを表示するかどうか</param>
        /// <param name="manageBusyState">処理中フラグとキャンセルソースのライフサイクルをこのメソッドで管理するかどうか</param>
        /// <returns>非同期操作のタスク</returns>
        private async Task ConvertAsync(
            IMainViewModelHost host,
            long operationGeneration,
            bool showFieldValidation = false,
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

            host.SetStatus("プレビュー生成中...", StatusKind.Progress);

            try
            {
                if (!int.TryParse(host.PageNumber, out int pageNumber) || pageNumber < 1)
                {
                    if (!IsCurrentPreviewOperation(operationGeneration))
                    {
                        return;
                    }

                    if (showFieldValidation)
                    {
                        host.PageNumberValidationMessage = "1 以上のページ番号を入力してください。";
                    }

                    host.SetStatus("有効なページ番号を入力してください。", StatusKind.Warning);
                    return;
                }

                host.PageNumberValidationMessage = null;

                if (!CoordinatorHelpers.TryGetResolutionValue(host, out double val, showFieldValidation))
                {
                    return;
                }

                if (!IsCurrentPreviewOperation(operationGeneration))
                {
                    return;
                }

                string pdfPath = await ResolvePdfPathAsync(host, host.FilePath, operationGeneration, cancellationToken);
                if (pdfPath == null)
                {
                    return;
                }

                int pageIndex = pageNumber - 1;
                var previewImage = await _pdfService.ConvertPdfPageToImageAsync(
                    pdfPath,
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
                host.SetStatus("プレビューを更新しました。", StatusKind.Success);
            }
            catch (Exception ex) when (CancellationExceptionHelper.IsOrContainsCancellation(ex))
            {
                if (IsCurrentPreviewOperation(operationGeneration))
                {
                    host.SetStatus("プレビュー変換をキャンセルしました。", StatusKind.Info);
                }
            }
            catch (Exception ex)
            {
                if (!IsCurrentPreviewOperation(operationGeneration))
                {
                    return;
                }

                host.SetStatus($"プレビューの生成中にエラーが発生しました: {ex.Message}", StatusKind.Error);
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

        /// <summary>
        /// 入力ドキュメントからPDFレンダリング用のパスを取得する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <param name="sourcePath">入力ファイルの絶対パス</param>
        /// <param name="operationGeneration">操作世代番号</param>
        /// <param name="cancellationToken">処理をキャンセルするためのトークン</param>
        /// <returns>PDFレンダリング用の絶対パス。失敗時は <c>null</c></returns>
        private async Task<string> ResolvePdfPathAsync(
            IMainViewModelHost host,
            string sourcePath,
            long operationGeneration,
            CancellationToken cancellationToken)
        {
            bool isWordDocument = DocumentFileHelper.IsWordFile(sourcePath);
            if (isWordDocument)
            {
                host.SetStatus("WordをPDFに変換中...", StatusKind.Progress);
            }

            try
            {
                string pdfPath = await _documentPdfSourceService.GetPdfPathAsync(sourcePath, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (!IsCurrentPreviewOperation(operationGeneration))
                {
                    return null;
                }

                return pdfPath;
            }
            catch (Exception ex) when (CancellationExceptionHelper.IsOrContainsCancellation(ex))
            {
                throw;
            }
            catch (Exception ex)
            {
                if (!IsCurrentPreviewOperation(operationGeneration))
                {
                    return null;
                }

                host.PageCount = 0;
                host.PreviewImage = null;
                host.LoadedFilePath = null;
                host.SetStatus(
                    isWordDocument
                        ? $"WordをPDFに変換に失敗しました: {ex.Message}"
                        : $"PDFの読み込みに失敗しました: {ex.Message}",
                    StatusKind.Error);
                return null;
            }
        }
    }
}
