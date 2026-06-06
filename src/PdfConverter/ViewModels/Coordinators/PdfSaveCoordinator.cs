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
    /// PDFページの一括保存と上書き確認を担当する
    /// </summary>
    internal sealed class PdfSaveCoordinator
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
        /// 指定したサービスを使用して PDF ページの一括保存と上書き確認を担当する
        /// </summary>
        /// <param name="pdfService">PDF 変換サービス</param>
        /// <param name="dialogService">ダイアログサービス</param>
        public PdfSaveCoordinator(IPdfConversionService pdfService, IDialogService dialogService)
        {
            _pdfService = pdfService;
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
        public async Task SaveAsync(IMainViewModelHost host)
        {
            if (string.IsNullOrEmpty(host.FilePath))
            {
                return;
            }

            if (!CoordinatorHelpers.TryGetResolutionValue(host, _dialogService, out double resolutionValue, showDialog: true))
            {
                return;
            }

            host.ProgressValue = 0;
            host.PrepareCancellation();
            CancellationToken cancellationToken = host.GetCancellationToken();
            host.IsBusy = true;
            host.IsSaving = true;
            host.StatusMessage = "保存処理を開始しています...";

            try
            {
                string folderPath = _dialogService.ShowFolderBrowserDialog();
                if (folderPath == null)
                {
                    host.StatusMessage = "保存先フォルダーの選択がキャンセルされました。";
                    return;
                }

                IEnumerable<int> pageIndexes = null;
                if (!host.IsAllPagesSelected)
                {
                    if (string.IsNullOrWhiteSpace(host.PageRange))
                    {
                        _dialogService.ShowMessage("保存対象のページ番号または範囲を入力してください。");
                        host.StatusMessage = "保存対象のページ番号または範囲を指定してください。";
                        return;
                    }

                    try
                    {
                        pageIndexes = PageRangeParser.Parse(host.PageRange, host.PageCount);
                    }
                    catch (Exception ex)
                    {
                        _dialogService.ShowMessage($"ページ範囲の指定が不正です: {ex.Message}");
                        host.StatusMessage = "有効なページ範囲を入力してください。";
                        return;
                    }
                }

                if (!ConfirmOverwriteExistingFiles(host, folderPath, pageIndexes))
                {
                    host.StatusMessage = "保存処理はキャンセルされました。";
                    return;
                }

                var progress = new Progress<SaveProgressReport>(report =>
                {
                    host.ProgressValue = report.Percentage;
                    host.StatusMessage = report.Message;
                });

                await _pdfService.SavePdfPagesToImagesAsync(
                    host.FilePath,
                    pageIndexes,
                    folderPath,
                    host.IsAllPagesSelected,
                    host.ResolutionMode,
                    resolutionValue,
                    host.OutputImageFormat,
                    host.PreserveTransparency,
                    progress,
                    cancellationToken);
                host.ProgressValue = 100;
                _dialogService.ShowMessage("画像の保存が完了しました。");
                host.StatusMessage = "画像の保存が完了しました。";
            }
            catch (OperationCanceledException)
            {
                host.StatusMessage = "保存処理はキャンセルされました。";
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"画像保存エラー: {ex.Message}");
                host.StatusMessage = "保存処理中にエラーが発生しました。";
            }
            finally
            {
                host.IsSaving = false;
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
        /// <returns>上書き確認の結果</returns>
        private bool ConfirmOverwriteExistingFiles(IMainViewModelHost host, string folderPath, IEnumerable<int> pageIndexes)
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

            return _dialogService.ShowYesNo(message, "上書きの確認", DialogIcon.Warning);
        }
    }
}
