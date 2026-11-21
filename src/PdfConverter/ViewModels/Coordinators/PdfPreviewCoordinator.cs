using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using PdfConverter.Infrastructure;
using PdfConverter.Models;
using PdfConverter.Services;

namespace PdfConverter.ViewModels.Coordinators
{
    /// <summary>
    /// PDF 読み込み・プレビュー生成・ページナビゲーションを担当する
    /// </summary>
    /// <remarks>
    /// プレビューは出力解像度を上限に、プレビュー画像を表示する領域のサイズへ縮小して描画する
    /// </remarks>
    internal sealed class PdfPreviewCoordinator : IPdfPreviewCoordinator
    {
        /********************************************************************************/
        /*                                     定数                                     */
        /********************************************************************************/
        /// <summary>表示領域のサイズが未確定の場合に使うプレビュー画像の最大幅・高さ (px)</summary>
        internal const int FallbackPreviewMaxPixelSize = 2048;

        /// <summary>表示領域の拡大が落ち着いてからプレビューを描画し直すまでの待機時間 (ミリ秒)</summary>
        internal const int ViewportRefreshDelayMilliseconds = 300;

        /// <summary>表示領域の拡大でプレビューを描画し直す、表示中の画像に対する拡大率の下限</summary>
        private const double ViewportRefreshGrowthThreshold = 1.05;


        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>PDF 変換サービス</summary>
        private readonly IPdfConversionService _pdfService;

        /// <summary>入力ドキュメントから PDF ソースを提供するサービス</summary>
        private readonly IDocumentPdfSourceService _documentPdfSourceService;

        /// <summary>プレビュータスクの排他制御用オブジェクト</summary>
        private readonly object _previewTaskLock = new object();

        /// <summary>プレビュー操作世代番号</summary>
        private long _previewOperationGeneration;

        /// <summary>アクティブなプレビュータスク</summary>
        private Task _activePreviewTask = Task.CompletedTask;

        /// <summary>アクティブなプレビュー UI フィードバック</summary>
        private PreviewUiFeedback? _activePreviewUiFeedback;

        /// <summary>直近に表示したプレビューの描画条件と結果</summary>
        private PreviewRenderState? _lastPreview;

        /// <summary>表示領域のサイズ変更の通知の世代番号 (待機中に次の通知が来た再描画を取りやめるために使う)</summary>
        private long _viewportChangeGeneration;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 指定したサービスを使用して PDF 読み込み・プレビュー生成・ページナビゲーションを担当する
        /// </summary>
        /// <param name="pdfService">PDF 変換サービス</param>
        /// <param name="documentPdfSourceService">入力ドキュメントから PDF ソースを提供するサービス</param>
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
        /// <see cref="IPreviewCoordinatorHost.FilePath"/> の PDF を検証して読み込み、ページ数取得とプレビュー生成を開始する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <param name="forceReload">強制的に再読み込みするかどうか</param>
        public void LoadFromPath(IPreviewCoordinatorHost host, bool forceReload = false)
        {
            if (host.IsSaving)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(host.FilePath))
            {
                return;
            }

            SupportedDocumentPathError pathError =
                DocumentFileHelper.ResolveSupportedDocumentPath(host.FilePath, out string? normalizedPath);
            if (pathError != SupportedDocumentPathError.None)
            {
                host.PageCount = 0;
                host.PreviewImage = null;
                host.LoadedFilePath = null;
                StatusKind statusKind = pathError == SupportedDocumentPathError.UnsupportedExtension
                    ? StatusKind.Warning
                    : StatusKind.Error;
                host.SetStatus(DocumentFileHelper.GetSupportedDocumentPathErrorMessage(pathError), statusKind);
                return;
            }

            host.FilePath = normalizedPath!;

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
            ApplicationLogger.Info("Preview", $"Loading document: {Path.GetFileName(host.FilePath)}");
            long operationGeneration = BeginPreviewOperation(host);
            TrackPreviewTask(LoadPageCountAndConvertAsync(host, operationGeneration));
        }

        /// <summary>
        /// PDF が読み込み済みのとき、現在の解像度設定でプレビューを再生成する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <returns>非同期操作のタスク</returns>
        public Task RefreshIfLoadedAsync(IPreviewCoordinatorHost host)
        {
            if (string.IsNullOrEmpty(host.FilePath) || host.PageCount <= 0 || host.IsSaving)
            {
                return Task.CompletedTask;
            }

            long operationGeneration = BeginPreviewOperation(host);
            return ConvertAsync(host, operationGeneration, showFieldValidation: false, manageBusyState: true);
        }

        /// <summary>
        /// 読み込み済み PDF のプレビュー再生成をスケジュールする
        /// </summary>
        /// <remarks>プロパティセッターなど UI スレッドから呼び出す用途向け</remarks>
        /// <param name="host">メインビューモデル</param>
        public void RequestRefreshIfLoaded(IPreviewCoordinatorHost host)
        {
            TrackPreviewTask(RefreshIfLoadedAsync(host));
        }

        /// <summary>
        /// 指定されたページに移動し、プレビューを更新する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <returns>非同期操作のタスク</returns>
        public async Task GoToPageAsync(IPreviewCoordinatorHost host)
        {
            if (host.PageCount <= 0)
            {
                host.SetStatus("ページ数が不明です。PDF を読み込んでください。", StatusKind.Warning);
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
        public async Task GoToPreviousPageAsync(IPreviewCoordinatorHost host)
        {
            if (host.CurrentPreviewPage <= 1)
            {
                return;
            }

            host.PageNumber = (host.CurrentPreviewPage - 1).ToString(CultureInfo.CurrentCulture);
            long operationGeneration = BeginPreviewOperation(host);
            await ConvertAsync(host, operationGeneration, showFieldValidation: true, manageBusyState: true);
        }

        /// <summary>
        /// 次のページに移動し、プレビューを更新する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <returns>非同期操作のタスク</returns>
        public async Task GoToNextPageAsync(IPreviewCoordinatorHost host)
        {
            if (host.CurrentPreviewPage >= host.PageCount)
            {
                return;
            }

            host.PageNumber = (host.CurrentPreviewPage + 1).ToString(CultureInfo.CurrentCulture);
            long operationGeneration = BeginPreviewOperation(host);
            await ConvertAsync(host, operationGeneration, showFieldValidation: true, manageBusyState: true);
        }

        /// <summary>
        /// プレビュー画像を表示する領域のサイズ変更を通知し、表示中のプレビューが粗くなる場合は少し待ってから描画し直す
        /// </summary>
        /// <remarks>
        /// ウィンドウのリサイズ中に描画を繰り返さないよう、最後の通知から <see cref="ViewportRefreshDelayMilliseconds"/> 待ってから描画する<br/>
        /// 表示領域が縮んだ場合は、表示中の画像を縮小表示すれば足りるため描画し直さない
        /// </remarks>
        /// <param name="host">メインビューモデル</param>
        public void NotifyPreviewViewportChanged(IPreviewCoordinatorHost host)
        {
            if (!NeedsSharperPreview(host))
            {
                return;
            }

            long viewportChangeGeneration = Interlocked.Increment(ref _viewportChangeGeneration);
            TrackPreviewTask(RefreshAfterViewportSettlesAsync(host, viewportChangeGeneration));
        }

        /// <summary>
        /// 表示中のプレビューと同じページ・設定の画像を出力解像度で取得する
        /// </summary>
        /// <remarks>
        /// プレビューが表示サイズに縮小されている場合のみ出力解像度で描画し直し、それ以外は表示中の画像をそのまま返す<br/>
        /// 描画し直す間は処理中とし、キャンセル操作で中断できる
        /// </remarks>
        /// <param name="host">メインビューモデル</param>
        /// <returns>出力解像度の画像。プレビューがない・キャンセル・失敗の場合は <c>null</c> (キャンセル・失敗時のステータスは設定済み)</returns>
        public async Task<BitmapSource?> GetOutputResolutionPreviewImageAsync(IPreviewCoordinatorHost host)
        {
            BitmapSource? displayedImage = host.PreviewImage;
            PreviewRenderState? lastPreview = _lastPreview;
            if (displayedImage == null
                || lastPreview == null
                || !ReferenceEquals(displayedImage, lastPreview.Preview.Image)
                || !lastPreview.Preview.IsDownscaled)
            {
                return displayedImage;
            }

            long operationGeneration = BeginPreviewOperation(host);
            CancellationToken cancellationToken = host.GetCancellationToken();
            host.IsBusy = true;
            host.SetStatus("出力解像度の画像を生成中...", StatusKind.Progress);

            try
            {
                BitmapSource image = await _pdfService.ConvertPdfPageToImageAsync(
                    lastPreview.PdfPath,
                    lastPreview.PageIndex,
                    lastPreview.ResolutionMode,
                    lastPreview.ResolutionValue,
                    lastPreview.PreserveTransparency,
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                return IsCurrentPreviewOperation(operationGeneration) ? image : null;
            }
            catch (Exception ex) when (CancellationExceptionHelper.IsOrContainsCancellation(ex))
            {
                if (IsCurrentPreviewOperation(operationGeneration))
                {
                    host.SetStatus("画像の生成をキャンセルしました。", StatusKind.Info);
                }

                return null;
            }
            catch (Exception ex)
            {
                if (IsCurrentPreviewOperation(operationGeneration))
                {
                    ApplicationLogger.Error("Preview", "Failed to render output resolution image", ex);
                    host.SetStatus($"出力解像度の画像の生成に失敗しました: {ExceptionFormattingHelper.GetUserMessage(ex)}", StatusKind.Error);
                }

                return null;
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


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// 新しいプレビュー操作を開始し、進行中の操作をキャンセルする
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <returns>今回の操作世代番号</returns>
        private long BeginPreviewOperation(IPreviewCoordinatorHost host)
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
        /// PDF のページ数を取得し、プレビューを生成する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <param name="operationGeneration">操作世代番号</param>
        /// <returns>非同期操作のタスク</returns>
        private async Task LoadPageCountAndConvertAsync(IPreviewCoordinatorHost host, long operationGeneration)
        {
            if (string.IsNullOrEmpty(host.FilePath))
            {
                return;
            }

            host.ProgressValue = 0;
            host.IsBusy = true;
            host.IsProgressBarVisible = true;
            CancellationToken cancellationToken = host.GetCancellationToken();
            string loadingPath = host.FilePath;

            try
            {
                try
                {
                    string? pdfPath = await ResolvePdfPathAsync(host, loadingPath, operationGeneration, cancellationToken);
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
                    ApplicationLogger.Info("Preview", $"Document loaded: {pageCount} page(s), file={Path.GetFileName(loadingPath)}");
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
                    ApplicationLogger.Error("Preview", "Failed to load document", ex);
                    host.SetStatus($"PDF の読み込みに失敗しました: {ExceptionFormattingHelper.GetUserMessage(ex)}", StatusKind.Error);
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
                    host.IsProgressBarVisible = false;
                    host.DisposeCancellation();
                }
            }
        }

        /// <summary>
        /// PDF のページを画像に変換し、プレビューを更新する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <param name="operationGeneration">操作世代番号</param>
        /// <param name="showFieldValidation">入力欄の検証メッセージを表示するかどうか</param>
        /// <param name="manageBusyState">処理中フラグとキャンセルソースのライフサイクルをこのメソッドで管理するかどうか</param>
        /// <returns>非同期操作のタスク</returns>
        private async Task ConvertAsync(
            IPreviewCoordinatorHost host,
            long operationGeneration,
            bool showFieldValidation = false,
            bool manageBusyState = true)
        {
            if (string.IsNullOrEmpty(host.FilePath))
            {
                return;
            }

            host.PrepareCancellation();
            CancellationToken cancellationToken = host.GetCancellationToken();

            PreviewUiFeedback? uiFeedback = null;
            if (manageBusyState)
            {
                _activePreviewUiFeedback?.Abandon();
                uiFeedback = new PreviewUiFeedback(host, () => IsCurrentPreviewOperation(operationGeneration));
                _activePreviewUiFeedback = uiFeedback;
            }
            else
            {
                host.SetStatus("プレビュー生成中...", StatusKind.Progress);
            }

            try
            {
                if (!int.TryParse(host.PageNumber, out int pageNumber) || pageNumber < 1)
                {
                    if (!IsCurrentPreviewOperation(operationGeneration))
                    {
                        return;
                    }

                    uiFeedback?.Abandon();

                    if (showFieldValidation)
                    {
                        host.PageNumberValidationMessage = "1 以上のページ番号を入力してください。";
                    }

                    host.SetStatus("有効なページ番号を入力してください。", StatusKind.Warning);
                    return;
                }

                host.PageNumberValidationMessage = null;

                if (!CoordinatorHelpers.TryGetResolutionValue(host, host, out double val, showFieldValidation))
                {
                    uiFeedback?.Abandon();
                    return;
                }

                if (!IsCurrentPreviewOperation(operationGeneration))
                {
                    return;
                }

                string? pdfPath = await ResolvePdfPathAsync(host, host.FilePath, operationGeneration, cancellationToken);
                if (pdfPath == null)
                {
                    uiFeedback?.Abandon();
                    return;
                }

                int pageIndex = pageNumber - 1;
                ResolutionMode resolutionMode = host.ResolutionMode;
                bool preserveTransparency = host.PreserveTransparency;
                (int maxPixelWidth, int maxPixelHeight) = ResolvePreviewMaxPixelSize(host);
                PdfPagePreview preview = await _pdfService.RenderPdfPagePreviewAsync(
                    pdfPath,
                    pageIndex,
                    resolutionMode,
                    val,
                    preserveTransparency,
                    maxPixelWidth,
                    maxPixelHeight,
                    cancellationToken);

                if (!IsCurrentPreviewOperation(operationGeneration))
                {
                    return;
                }

                _lastPreview = new PreviewRenderState(pdfPath, pageIndex, resolutionMode, val, preserveTransparency, preview);
                host.PreviewImage = preview.Image;
                if (manageBusyState)
                {
                    uiFeedback?.CompleteSuccess();
                }
                else
                {
                    host.SetStatus("プレビューを更新しました。", StatusKind.Success);
                }
            }
            catch (Exception ex) when (CancellationExceptionHelper.IsOrContainsCancellation(ex))
            {
                if (IsCurrentPreviewOperation(operationGeneration))
                {
                    if (manageBusyState)
                    {
                        uiFeedback?.CompleteCancelled();
                    }
                    else
                    {
                        host.SetStatus("プレビュー変換をキャンセルしました。", StatusKind.Info);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!IsCurrentPreviewOperation(operationGeneration))
                {
                    return;
                }

                if (manageBusyState)
                {
                    uiFeedback?.CompleteError($"プレビューの生成中にエラーが発生しました: {ExceptionFormattingHelper.GetUserMessage(ex)}");
                }
                else
                {
                    host.SetStatus($"プレビューの生成中にエラーが発生しました: {ExceptionFormattingHelper.GetUserMessage(ex)}", StatusKind.Error);
                }

                ApplicationLogger.Error("Preview", "Failed to render preview", ex);
            }
            finally
            {
                if (manageBusyState)
                {
                    uiFeedback?.Dispose();
                    if (_activePreviewUiFeedback == uiFeedback)
                    {
                        _activePreviewUiFeedback = null;
                    }

                    if (IsCurrentPreviewOperation(operationGeneration))
                    {
                        host.DisposeCancellation();
                    }
                }
            }
        }

        /// <summary>
        /// 入力ドキュメントから PDF レンダリング用のパスを取得する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <param name="sourcePath">入力ファイルの絶対パス</param>
        /// <param name="operationGeneration">操作世代番号</param>
        /// <param name="cancellationToken">処理をキャンセルするためのトークン</param>
        /// <returns>PDF レンダリング用の絶対パス。失敗時は <c>null</c></returns>
        private async Task<string?> ResolvePdfPathAsync(
            IPreviewCoordinatorHost host,
            string sourcePath,
            long operationGeneration,
            CancellationToken cancellationToken)
        {
            bool isWordDocument = DocumentFileHelper.IsWordFile(sourcePath);
            if (isWordDocument)
            {
                CoordinatorHelpers.ReportWordToPdfConversionProgress(host);
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
                ApplicationLogger.Error("Preview", isWordDocument ? "Word to PDF conversion failed" : "Failed to open PDF", ex);
                host.SetStatus(
                    isWordDocument
                        ? $"Word から PDF への変換に失敗しました: {ExceptionFormattingHelper.GetUserMessage(ex)}"
                        : $"PDF の読み込みに失敗しました: {ExceptionFormattingHelper.GetUserMessage(ex)}",
                    StatusKind.Error);
                return null;
            }
        }

        /// <summary>
        /// プレビュー画像の最大サイズを、表示領域のサイズ (未確定の場合は既定値) から決める
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <returns>最大幅と最大高さ (px)</returns>
        private static (int Width, int Height) ResolvePreviewMaxPixelSize(IPreviewCoordinatorHost host)
        {
            int width = host.PreviewViewportPixelWidth;
            int height = host.PreviewViewportPixelHeight;
            if (width <= 0 || height <= 0)
            {
                return (FallbackPreviewMaxPixelSize, FallbackPreviewMaxPixelSize);
            }

            return (width, height);
        }

        /// <summary>
        /// 表示中のプレビューが縮小描画されており、現在の表示領域ならより大きく描画できるかどうかを判定する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <returns>true: 描画し直すと鮮明になる / false: 描画し直す必要がない</returns>
        private bool NeedsSharperPreview(IPreviewCoordinatorHost host)
        {
            PreviewRenderState? lastPreview = _lastPreview;
            if (lastPreview == null
                || !lastPreview.Preview.IsDownscaled
                || !ReferenceEquals(host.PreviewImage, lastPreview.Preview.Image))
            {
                return false;
            }

            (int maxPixelWidth, int maxPixelHeight) = ResolvePreviewMaxPixelSize(host);
            BitmapSource image = lastPreview.Preview.Image;
            double growth = Math.Min(
                (double)maxPixelWidth / image.PixelWidth,
                (double)maxPixelHeight / image.PixelHeight);
            return growth >= ViewportRefreshGrowthThreshold;
        }

        /// <summary>
        /// 表示領域のサイズ変更が落ち着くのを待ってから、プレビューを描画し直す
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <param name="viewportChangeGeneration">通知時に取得した世代番号</param>
        /// <returns>非同期操作のタスク</returns>
        private async Task RefreshAfterViewportSettlesAsync(IPreviewCoordinatorHost host, long viewportChangeGeneration)
        {
            await Task.Delay(ViewportRefreshDelayMilliseconds);

            // 待機中に次のサイズ変更があった場合は、その通知の待機に任せる
            if (viewportChangeGeneration != Volatile.Read(ref _viewportChangeGeneration))
            {
                return;
            }

            // 読み込み・保存・出力解像度の画像の生成などの処理中は割り込まない
            if (host.IsBusy || !NeedsSharperPreview(host))
            {
                return;
            }

            await RefreshIfLoadedAsync(host);
        }


        /********************************************************************************/
        /*                                    ネスト型                                  */
        /********************************************************************************/
        /// <summary>
        /// 表示したプレビューの描画条件と結果を保持する
        /// </summary>
        private sealed class PreviewRenderState
        {
            /// <summary>
            /// 描画条件と結果を指定して初期化する
            /// </summary>
            /// <param name="pdfPath">描画した PDF の絶対パス</param>
            /// <param name="pageIndex">描画したページインデックス (0 始まり)</param>
            /// <param name="resolutionMode">解像度の指定方法</param>
            /// <param name="resolutionValue">解像度の値</param>
            /// <param name="preserveTransparency">透明度を保持したかどうか</param>
            /// <param name="preview">描画したプレビュー</param>
            public PreviewRenderState(
                string pdfPath,
                int pageIndex,
                ResolutionMode resolutionMode,
                double resolutionValue,
                bool preserveTransparency,
                PdfPagePreview preview)
            {
                PdfPath = pdfPath;
                PageIndex = pageIndex;
                ResolutionMode = resolutionMode;
                ResolutionValue = resolutionValue;
                PreserveTransparency = preserveTransparency;
                Preview = preview;
            }

            /// <summary>描画した PDF の絶対パス</summary>
            public string PdfPath { get; }

            /// <summary>描画したページインデックス (0 始まり)</summary>
            public int PageIndex { get; }

            /// <summary>解像度の指定方法</summary>
            public ResolutionMode ResolutionMode { get; }

            /// <summary>解像度の値</summary>
            public double ResolutionValue { get; }

            /// <summary>透明度を保持したかどうか</summary>
            public bool PreserveTransparency { get; }

            /// <summary>描画したプレビュー</summary>
            public PdfPagePreview Preview { get; }
        }
    }
}
