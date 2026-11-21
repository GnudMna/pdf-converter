using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using PdfConverter.Commands;
using PdfConverter.Models;
using PdfConverter.Services;
using PdfConverter.Tests.Helpers;
using PdfConverter.ViewModels;
using PdfConverter.ViewModels.Coordinators;
using Xunit;

namespace PdfConverter.Tests.ViewModels
{
    /// <summary>
    /// <see cref="MainViewModel"/> のプロパティ・コマンド・PDF 操作フローを検証する
    /// </summary>
    public class MainViewModelTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// ファイルパス未設定時に PageIndicator が選択を促すメッセージを返すことを検証する
        /// </summary>
        [Fact]
        public void PageIndicator_WhenNoFilePath_ShowsPrompt()
        {
            var (viewModel, _, _, _) = MainViewModelTestFactory.Create();

            Assert.Equal("ファイルを選択してください", viewModel.Preview.PageIndicator);
        }

        /// <summary>
        /// ファイルパスは設定済みだがページ数未取得のときに読み込み中メッセージを返すことを検証する
        /// </summary>
        [Fact]
        public void PageIndicator_WhenLoading_ShowsLoadingMessage()
        {
            var (viewModel, _, _, _) = MainViewModelTestFactory.Create();
            viewModel.FilePath = "C:\\sample.pdf";
            viewModel.Preview.PageCount = 0;

            Assert.Equal("ページ数を読み込んでいます...", viewModel.Preview.PageIndicator);
        }

        /// <summary>
        /// PDF 読み込み完了後に現在ページ/総ページ数の形式で PageIndicator が表示されることを検証する
        /// </summary>
        [Fact]
        public void PageIndicator_WhenLoaded_ShowsCurrentPage()
        {
            var (viewModel, _, _, _) = MainViewModelTestFactory.Create();
            viewModel.FilePath = "C:\\sample.pdf";
            viewModel.Preview.PageCount = 5;
            viewModel.Preview.PageNumber = "3";

            Assert.Equal("3/5 ページ", viewModel.Preview.PageIndicator);
        }

        /// <summary>
        /// ページ数が 0 のときはページ移動 UI を非表示にすることを検証する
        /// </summary>
        [Fact]
        public void IsPageNavigationVisible_WhenNoPages_IsFalse()
        {
            var (viewModel, _, _, _) = MainViewModelTestFactory.Create();

            Assert.False(viewModel.Preview.IsPageNavigationVisible);

            viewModel.Preview.PageCount = 3;

            Assert.True(viewModel.Preview.IsPageNavigationVisible);
        }

        /// <summary>
        /// 総ページ数の桁数に応じてページ番号入力欄の最小幅が広がることを検証する
        /// </summary>
        [Fact]
        public void PageNumberInputMinWidth_ExpandsForLargePageCount()
        {
            var (viewModel, _, _, _) = MainViewModelTestFactory.Create();
            viewModel.Preview.PageCount = 12;
            viewModel.Preview.PageNumber = "1";

            var narrowWidth = viewModel.Preview.PageNumberInputMinWidth;

            viewModel.Preview.PageCount = 1234;
            viewModel.Preview.PageNumber = "1234";

            Assert.True(viewModel.Preview.PageNumberInputMinWidth > narrowWidth);
            Assert.True(viewModel.Preview.PageCountDisplayMinWidth > 16);
        }

        /// <summary>
        /// 存在しないファイルを読み込もうとした場合にエラーステータスが設定され、PDF サービスが呼ばれないことを検証する
        /// </summary>
        [Fact]
        public void LoadPdfFromPath_MissingFile_SetsErrorStatus()
        {
            var (viewModel, pdf, _, _) = MainViewModelTestFactory.Create();
            viewModel.FilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");

            viewModel.LoadPdfFromPath();

            Assert.Equal(0, viewModel.Preview.PageCount);
            Assert.Contains("見つかりません", viewModel.StatusMessage);
            pdf.Verify(p => p.GetPdfPageCountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        /// <summary>
        /// .pdf 以外の拡張子ではエラーメッセージが設定され、PDF サービスが呼ばれないことを検証する
        /// </summary>
        [Fact]
        public void LoadPdfFromPath_InvalidExtension_DoesNotCallPdfService()
        {
            var (viewModel, pdf, _, _) = MainViewModelTestFactory.Create();
            string path = Path.GetTempFileName();
            viewModel.FilePath = path;

            try
            {
                viewModel.LoadPdfFromPath(forceReload: true);

                Assert.Contains(".pdf", viewModel.StatusMessage);
                pdf.Verify(p => p.GetPdfPageCountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// BrowseCommand 実行時にファイル選択ダイアログが開き、選択パスが FilePath に設定されることを検証する
        /// </summary>
        [Fact]
        public void BrowseCommand_SetsFilePathAndLoadsPdf()
        {
            var (viewModel, pdf, dialog, _) = MainViewModelTestFactory.Create();
            string selectedPath = CreateTempPdfPath();
            dialog.Setup(d => d.ShowOpenDocumentFileDialog()).Returns(selectedPath);
            pdf.Setup(p => p.GetPdfPageCountAsync(selectedPath, It.IsAny<CancellationToken>())).ReturnsAsync(1);
            pdf.Setup(p => p.RenderPdfPagePreviewAsync(
                    selectedPath,
                    0,
                    It.IsAny<ResolutionMode>(),
                    It.IsAny<double>(),
                    It.IsAny<bool>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var bitmap = CreatePreviewBitmap();
                    return new PdfPagePreview(bitmap, bitmap.PixelWidth, bitmap.PixelHeight);
                });

            try
            {
                StaTestHelper.Run(() => viewModel.BrowseCommand.Execute(null));

                Assert.Equal(selectedPath, viewModel.FilePath);
                dialog.Verify(d => d.ShowOpenDocumentFileDialog(), Times.Once);
            }
            finally
            {
                File.Delete(selectedPath);
            }
        }

        /// <summary>
        /// プレビュー画像がない状態ではコピーコマンドが実行不可で、実行してもクリップボード操作が行われないことを検証する
        /// </summary>
        [Fact]
        public void CopyToClipboardCommand_WithoutPreview_CannotExecuteAndDoesNotCopy()
        {
            var (viewModel, _, _, clipboard) = MainViewModelTestFactory.Create();
            viewModel.Preview.PreviewImage = null;

            StaTestHelper.Run(() => viewModel.CopyToClipboardCommand.Execute(null));

            Assert.False(viewModel.CopyToClipboardCommand.CanExecute(null));
            clipboard.Verify(c => c.CopyImage(It.IsAny<System.Windows.Media.Imaging.BitmapSource>(), It.IsAny<bool>()), Times.Never);
        }

        /// <summary>
        /// プレビュー画像がある状態でコピーコマンドを実行した場合にクリップボードへ画像がコピーされることを検証する
        /// </summary>
        [Fact]
        public void CopyToClipboardCommand_WithPreview_CopiesImage()
        {
            var (viewModel, _, _, clipboard) = MainViewModelTestFactory.Create();
            System.Windows.Media.Imaging.BitmapSource image = null;
            StaTestHelper.Run(() => image = BitmapTestHelper.CreateBitmap());
            viewModel.ExportSettings.OutputImageFormat = OutputImageFormat.Png;
            viewModel.ExportSettings.PreserveTransparency = true;
            viewModel.Preview.PreviewImage = image;

            StaTestHelper.Run(() => viewModel.CopyToClipboardCommand.Execute(null));

            clipboard.Verify(c => c.CopyImage(image, true), Times.Once);
            Assert.Contains("コピーしました", viewModel.StatusMessage);
        }

        /// <summary>
        /// ファイルパス未設定時に SaveCommand が実行不可であることを検証する
        /// </summary>
        [Fact]
        public void SaveCommand_WhenFilePathEmpty_CannotExecute()
        {
            var (viewModel, _, _, _) = MainViewModelTestFactory.Create();
            viewModel.FilePath = null;

            Assert.False(viewModel.SaveCommand.CanExecute(null));
        }

        /// <summary>
        /// ファイルパス設定済みかつ非 Busy 状態で SaveCommand が実行可能であることを検証する
        /// </summary>
        [Fact]
        public void SaveCommand_WhenFilePathSetAndIdle_CanExecute()
        {
            var (viewModel, _, _, _) = MainViewModelTestFactory.Create();
            viewModel.FilePath = "C:\\sample.pdf";
            viewModel.IsBusy = false;

            Assert.True(viewModel.SaveCommand.CanExecute(null));
        }

        /// <summary>
        /// 処理中 (IsBusy) のときに BrowseCommand が実行不可能であることを検証する
        /// </summary>
        [Fact]
        public void BrowseCommand_WhenBusy_CannotExecute()
        {
            var (viewModel, _, _, _) = MainViewModelTestFactory.Create();
            viewModel.IsBusy = true;

            ((IRelayCommand)viewModel.BrowseCommand).RaiseCanExecuteChanged();
            Assert.False(viewModel.BrowseCommand.CanExecute(null));
        }

        /// <summary>
        /// 処理中 (IsBusy) のときに CancelCommand が実行可能であることを検証する
        /// </summary>
        [Fact]
        public void CancelCommand_WhenBusy_CanExecute()
        {
            var (viewModel, _, _, _) = MainViewModelTestFactory.Create();
            viewModel.IsBusy = true;

            ((IRelayCommand)viewModel.CancelCommand).RaiseCanExecuteChanged();
            Assert.True(viewModel.CancelCommand.CanExecute(null));
        }

        /// <summary>
        /// プロパティ変更時に PropertyChanged イベントが発火することを検証する
        /// </summary>
        [Fact]
        public void SetProperty_RaisesPropertyChanged()
        {
            var (viewModel, _, _, _) = MainViewModelTestFactory.Create();
            var raised = false;
            viewModel.Status.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == "StatusMessage")
                {
                    raised = true;
                }
            };

            viewModel.SetStatus("updated");

            Assert.True(raised);
            Assert.Equal("updated", viewModel.StatusMessage);
        }

        /// <summary>
        /// SaveCommand 実行時に保存フローが完了し、完了ステータスが設定されることを検証する
        /// </summary>
        [Fact]
        public async Task SaveCommand_ExecutesSaveFlow()
        {
            var (viewModel, pdf, dialog, _) = MainViewModelTestFactory.Create();
            string folder = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(folder);
            viewModel.FilePath = "C:\\sample.pdf";
            viewModel.Preview.PageCount = 1;
            viewModel.ExportSettings.PageRange = "1";
            viewModel.ExportSettings.ResolutionValue = "1080";
            dialog.Setup(d => d.ShowFolderBrowserDialog()).Returns(folder);
            pdf.Setup(p => p.GetPdfPageCountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
            pdf.Setup(p => p.SavePdfPagesToImagesAsync(
                    It.IsAny<string>(),
                    It.IsAny<System.Collections.Generic.IEnumerable<int>>(),
                    folder,
                    false,
                    It.IsAny<ResolutionMode>(),
                    1080,
                    It.IsAny<OutputImageFormat>(),
                    It.IsAny<bool>(),
                    It.IsAny<IProgress<Models.SaveProgressReport>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            viewModel.SaveCommand.Execute(null);
            await Task.Delay(300, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Contains("完了", viewModel.StatusMessage);
            Directory.Delete(folder, recursive: true);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        private static System.Windows.Media.Imaging.BitmapSource CreatePreviewBitmap()
        {
            System.Windows.Media.Imaging.BitmapSource image = null;
            StaTestHelper.Run(() => image = BitmapTestHelper.CreateBitmap());
            return image;
        }

        private static string CreateTempPdfPath()
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.pdf");
            File.WriteAllText(path, "placeholder");
            return path;
        }

        /// <summary>
        /// 処理中でないとき LoadPdfFromPathCommand が実行可能であることを検証する
        /// </summary>
        [Fact]
        public void LoadPdfFromPathCommand_WhenIdle_CanExecute()
        {
            var (viewModel, _, _, _) = MainViewModelTestFactory.Create();

            Assert.True(viewModel.LoadPdfFromPathCommand.CanExecute(null));
        }

        /// <summary>
        /// 処理中は LoadPdfFromPathCommand が実行不可であることを検証する
        /// </summary>
        [Fact]
        public void LoadPdfFromPathCommand_WhenBusy_CannotExecute()
        {
            var (viewModel, _, _, _) = MainViewModelTestFactory.Create();
            viewModel.IsBusy = true;

            Assert.False(viewModel.LoadPdfFromPathCommand.CanExecute(null));
        }

        /// <summary>
        /// OpenUserDocsCommand がダイアログサービスへドキュメントの表示を委譲することを検証する
        /// </summary>
        [Fact]
        public void OpenUserDocsCommand_OpensUserDocs()
        {
            var (viewModel, _, dialog, _) = MainViewModelTestFactory.Create();

            viewModel.OpenUserDocsCommand.Execute(null);

            dialog.Verify(d => d.OpenUserDocs(), Times.Once);
        }

        /// <summary>
        /// ドロップされた PDF パスが FilePath に設定されることを検証する
        /// </summary>
        [Fact]
        public void HandleDroppedDocument_SetsFilePath()
        {
            var (viewModel, _, _, _) = MainViewModelTestFactory.Create();
            string pdfPath = CreateTempPdfPath();

            try
            {
                viewModel.HandleDroppedDocument(pdfPath);

                Assert.Equal(pdfPath, viewModel.FilePath);
            }
            finally
            {
                File.Delete(pdfPath);
            }
        }

        /// <summary>
        /// 処理中はドロップされた PDF を無視することを検証する
        /// </summary>
        [Fact]
        public void HandleDroppedDocument_WhenBusy_DoesNothing()
        {
            var (viewModel, _, _, _) = MainViewModelTestFactory.Create();
            viewModel.IsBusy = true;
            viewModel.FilePath = "C:\\existing.pdf";

            viewModel.HandleDroppedDocument("C:\\dropped.pdf");

            Assert.Equal("C:\\existing.pdf", viewModel.FilePath);
        }

        /// <summary>
        /// Word 文書の読み込み後に Word 設定を変えると、文書を再読み込みすることを検証する
        /// </summary>
        [Fact]
        public void WordSettingsChange_WhenWordDocumentLoaded_ReloadsDocument()
        {
            var previewCoordinator = new Mock<IPdfPreviewCoordinator>();
            MainViewModel viewModel = MainViewModelTestFactory.Create(previewCoordinator.Object);
            viewModel.FilePath = "C:\\document.docx";

            viewModel.WordSettings.ExportBookmarks = !viewModel.WordSettings.ExportBookmarks;

            previewCoordinator.Verify(c => c.LoadFromPath(It.IsAny<IPreviewCoordinatorHost>(), true), Times.Once);
        }

        /// <summary>
        /// 入力途中の値の反映で Word 設定が変わっても、文書を再読み込みしないことを検証する
        /// </summary>
        [Fact]
        public void CommitPendingInput_WordSettingsChange_DoesNotReloadDocument()
        {
            var previewCoordinator = new Mock<IPdfPreviewCoordinator>();
            MainViewModel viewModel = MainViewModelTestFactory.Create(previewCoordinator.Object);
            viewModel.FilePath = "C:\\document.docx";

            bool exportBookmarks = !viewModel.WordSettings.ExportBookmarks;

            viewModel.CommitPendingInput(() => viewModel.WordSettings.ExportBookmarks = exportBookmarks);

            Assert.Equal(exportBookmarks, viewModel.WordSettings.ExportBookmarks);
            previewCoordinator.Verify(c => c.LoadFromPath(It.IsAny<IPreviewCoordinatorHost>(), It.IsAny<bool>()), Times.Never);
        }

        /// <summary>
        /// 入力途中の値の反映で画像出力設定が変わっても、プレビューを再生成しないことを検証する
        /// </summary>
        [Fact]
        public void CommitPendingInput_ExportSettingsChange_DoesNotRefreshPreview()
        {
            var previewCoordinator = new Mock<IPdfPreviewCoordinator>();
            MainViewModel viewModel = MainViewModelTestFactory.Create(previewCoordinator.Object);

            viewModel.CommitPendingInput(() => viewModel.ExportSettings.ResolutionValue = "1234");

            previewCoordinator.Verify(c => c.RequestRefreshIfLoaded(It.IsAny<IPreviewCoordinatorHost>()), Times.Never);
        }

        /// <summary>
        /// 入力途中の値の反映が終わった後は、設定変更でプレビューを再生成することを検証する
        /// </summary>
        [Fact]
        public void ExportSettingsChange_AfterCommitPendingInput_RefreshesPreview()
        {
            var previewCoordinator = new Mock<IPdfPreviewCoordinator>();
            MainViewModel viewModel = MainViewModelTestFactory.Create(previewCoordinator.Object);
            viewModel.CommitPendingInput(() => viewModel.ExportSettings.ResolutionValue = "1234");

            viewModel.ExportSettings.ResolutionValue = "2345";

            previewCoordinator.Verify(c => c.RequestRefreshIfLoaded(It.IsAny<IPreviewCoordinatorHost>()), Times.Once);
        }

        /// <summary>
        /// 複数ファイルのドロップ時に読み込んだファイル名を含む通知ダイアログを表示することを検証する
        /// </summary>
        [Fact]
        public void NotifyMultipleFilesDropped_ShowsMessageWithLoadedFileName()
        {
            var (viewModel, _, dialog, _) = MainViewModelTestFactory.Create();

            viewModel.NotifyMultipleFilesDropped(3, "C:\\docs\\first.pdf");

            dialog.Verify(
                d => d.ShowMessage(
                    It.Is<string>(m => m.Contains("3 件") && m.Contains("first.pdf")),
                    It.IsAny<string>(),
                    DialogIcon.Information,
                    It.IsAny<string>()),
                Times.Once);
        }
    }
}
