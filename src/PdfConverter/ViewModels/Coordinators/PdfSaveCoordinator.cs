using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PdfConverter.Models;
using PdfConverter.Services;

namespace PdfConverter.ViewModels.Coordinators
{
    /// <summary>
    /// PDF ページの一括保存と上書き確認を担当する
    /// </summary>
    internal sealed class PdfSaveCoordinator : IPdfSaveCoordinator
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>PDF 変換サービス</summary>
        private readonly IPdfConversionService _pdfService;

        /// <summary>入力ドキュメントから PDF ソースを提供するサービス</summary>
        private readonly IDocumentPdfSourceService _documentPdfSourceService;

        /// <summary>ダイアログサービス</summary>
        private readonly IDialogService _dialogService;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 指定したサービスを使用して PDF ページの一括保存と上書き確認を担当する
        /// </summary>
        /// <param name="pdfService">PDF 変換サービス</param>
        /// <param name="documentPdfSourceService">入力ドキュメントから PDF ソースを提供するサービス</param>
        /// <param name="dialogService">ダイアログサービス</param>
        public PdfSaveCoordinator(
            IPdfConversionService pdfService,
            IDocumentPdfSourceService documentPdfSourceService,
            IDialogService dialogService)
        {
            _pdfService = pdfService;
            _documentPdfSourceService = documentPdfSourceService;
            _dialogService = dialogService;
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 指定ページを画像ファイルとして保存する
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <returns>非同期操作のタスク</returns>
        public async Task SaveAsync(ISaveCoordinatorHost host)
        {
            if (string.IsNullOrEmpty(host.FilePath))
            {
                return;
            }

            if (!CoordinatorHelpers.TryGetResolutionValue(host, host, out double resolutionValue, showFieldValidation: true))
            {
                return;
            }

            host.ProgressValue = 0;
            host.PrepareCancellation();
            CancellationToken cancellationToken = host.GetCancellationToken();
            host.IsBusy = true;
            host.IsSaving = true;
            host.SetStatus("保存処理を開始しています...", StatusKind.Progress);

            try
            {
                if (DocumentFileHelper.IsWordFile(host.FilePath))
                {
                    host.SetStatus("Word を PDF に変換中...", StatusKind.Progress);
                }

                string pdfPath = await _documentPdfSourceService.GetPdfPathAsync(host.FilePath, cancellationToken);

                string folderPath = _dialogService.ShowFolderBrowserDialog();
                if (folderPath == null)
                {
                    host.SetStatus("保存先フォルダーの選択がキャンセルされました。", StatusKind.Info);
                    return;
                }

                IEnumerable<int> pageIndexes = null;
                if (!host.IsAllPagesSelected)
                {
                    if (string.IsNullOrWhiteSpace(host.PageRange))
                    {
                        host.PageRangeValidationMessage = "保存対象のページ番号または範囲を入力してください。";
                        host.SetStatus("保存対象のページ番号または範囲を指定してください。", StatusKind.Warning);
                        return;
                    }

                    try
                    {
                        pageIndexes = PageRangeParser.Parse(host.PageRange, host.PageCount);
                        host.PageRangeValidationMessage = null;
                    }
                    catch (Exception ex)
                    {
                        host.PageRangeValidationMessage = ex.Message;
                        host.SetStatus($"ページ範囲の指定が不正です: {ex.Message}", StatusKind.Warning);
                        return;
                    }
                }
                else
                {
                    host.PageRangeValidationMessage = null;
                }

                if (!ConfirmOverwriteExistingFiles(host, folderPath, pageIndexes))
                {
                    host.SetStatus("保存処理はキャンセルされました。", StatusKind.Info);
                    return;
                }

                var progress = new Progress<SaveProgressReport>(report =>
                {
                    host.ProgressValue = report.Percentage;
                    host.SetStatus(report.Message, StatusKind.Progress);
                });

                await _pdfService.SavePdfPagesToImagesAsync(
                    pdfPath,
                    pageIndexes,
                    folderPath,
                    host.IsAllPagesSelected,
                    host.ResolutionMode,
                    resolutionValue,
                    host.OutputImageFormat,
                    host.PreserveTransparency,
                    progress,
                    cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    host.SetStatus("保存処理はキャンセルされました。", StatusKind.Info);
                    return;
                }

                host.ProgressValue = 100;
                host.SetStatus("画像の保存が完了しました。", StatusKind.Success);
            }
            catch (Exception ex) when (CancellationExceptionHelper.IsOrContainsCancellation(ex))
            {
                host.SetStatus("保存処理はキャンセルされました。", StatusKind.Info);
            }
            catch (Exception ex)
            {
                host.SetStatus($"保存処理中にエラーが発生しました: {ex.Message}", StatusKind.Error);
            }
            finally
            {
                host.IsSaving = false;
                host.IsBusy = false;
                host.DisposeCancellation();
            }
        }

        /// <inheritdoc/>
        public async Task SavePdfAsync(ISaveCoordinatorHost host)
        {
            if (string.IsNullOrEmpty(host.FilePath) || !DocumentFileHelper.IsWordFile(host.FilePath))
            {
                return;
            }

            host.PrepareCancellation();
            CancellationToken cancellationToken = host.GetCancellationToken();
            host.IsBusy = true;
            host.SetStatus("PDF 保存処理を開始しています...", StatusKind.Progress);

            try
            {
                host.SetStatus("Word を PDF に変換中...", StatusKind.Progress);
                string pdfPath = await _documentPdfSourceService.GetPdfPathAsync(host.FilePath, cancellationToken);

                string suggestedFileName = Path.GetFileNameWithoutExtension(host.FilePath) + ".pdf";
                string destinationPath = _dialogService.ShowSavePdfFileDialog(suggestedFileName);
                if (destinationPath == null)
                {
                    host.SetStatus("PDF の保存がキャンセルされました。", StatusKind.Info);
                    return;
                }

                if (File.Exists(destinationPath)
                    && !_dialogService.ShowYesNo(
                        $"以下のファイルは既に存在します。上書きしますか？\n\n{Path.GetFileName(destinationPath)}",
                        "上書きの確認",
                        DialogIcon.Warning,
                        "上書きする",
                        "キャンセル"))
                {
                    host.SetStatus("PDF の保存がキャンセルされました。", StatusKind.Info);
                    return;
                }

                File.Copy(pdfPath, destinationPath, overwrite: true);
                host.SetStatus($"PDF を保存しました: {destinationPath}", StatusKind.Success);
            }
            catch (Exception ex) when (CancellationExceptionHelper.IsOrContainsCancellation(ex))
            {
                host.SetStatus("PDF の保存はキャンセルされました。", StatusKind.Info);
            }
            catch (Exception ex)
            {
                host.SetStatus($"PDF の保存中にエラーが発生しました: {ex.Message}", StatusKind.Error);
            }
            finally
            {
                host.IsBusy = false;
                host.DisposeCancellation();
            }
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// 既存のファイルとの上書き確認を行う
        /// </summary>
        /// <param name="host">メインビューモデル</param>
        /// <param name="folderPath">保存先フォルダーのパス</param>
        /// <param name="pageIndexes">保存するページのインデックス</param>
        /// <returns>true: 上書き確認を行う / false: 上書き確認を行わない / キャンセルされた場合は<c>null</c></returns>
        private bool ConfirmOverwriteExistingFiles(ISaveCoordinatorHost host, string folderPath, IEnumerable<int> pageIndexes)
        {
            string extension = ImageBitmapHelper.GetFileExtension(host.OutputImageFormat);
            IEnumerable<int> pagesToSave = host.IsAllPagesSelected
                ? Enumerable.Range(0, host.PageCount)
                : pageIndexes;

            var existingFiles = pagesToSave
                .Select(index => Path.Combine(folderPath, $"page_{index + 1}{extension}"))
                .Where(File.Exists)
                .Select(Path.GetFileName)
                .ToList();

            if (existingFiles.Count == 0)
            {
                return true;
            }

            string message = existingFiles.Count <= 5
                ? $"以下のファイルは既に存在します。上書きしますか？\n\n{string.Join("\n", existingFiles)}"
                : $"{existingFiles.Count} 件のファイルが既に存在します。上書きしますか？";

            return _dialogService.ShowYesNo(message, "上書きの確認", DialogIcon.Warning, "上書きする", "キャンセル");
        }
    }
}
