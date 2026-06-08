using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using PdfConverter.Commands;
using PdfConverter.Models;
using PdfConverter.Tests.Helpers;
using Xunit;

namespace PdfConverter.Tests.ViewModels
{
    /// <summary>
    /// <see cref="MainViewModel"/> のプロパティ・コマンド・PDF 操作フローを検証する
    /// </summary>
    public class MainViewModelTests
    {
        /// <summary>
        /// ファイルパス未設定時に PageIndicator が選択を促すメッセージを返すことを検証する
        /// </summary>
        [Fact]
        public void PageIndicator_WhenNoFilePath_ShowsPrompt()
        {
            var (viewModel, _, _, _) = MainViewModelTestFactory.Create();

            viewModel.PageIndicator.Should().Be("ファイルを選択してください");
        }

        /// <summary>
        /// ファイルパスは設定済みだがページ数未取得のときに読み込み中メッセージを返すことを検証する
        /// </summary>
        [Fact]
        public void PageIndicator_WhenLoading_ShowsLoadingMessage()
        {
            var (viewModel, _, _, _) = MainViewModelTestFactory.Create();
            viewModel.FilePath = "C:\\sample.pdf";
            viewModel.PageCount = 0;

            viewModel.PageIndicator.Should().Be("ページ数を読み込んでいます...");
        }

        /// <summary>
        /// PDF 読み込み完了後に現在ページ/総ページ数の形式で PageIndicator が表示されることを検証する
        /// </summary>
        [Fact]
        public void PageIndicator_WhenLoaded_ShowsCurrentPage()
        {
            var (viewModel, _, _, _) = MainViewModelTestFactory.Create();
            viewModel.FilePath = "C:\\sample.pdf";
            viewModel.PageCount = 5;
            viewModel.PageNumber = "3";

            viewModel.PageIndicator.Should().Be("3/5 ページ");
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

            viewModel.PageCount.Should().Be(0);
            viewModel.StatusMessage.Should().Contain("見つかりません");
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

                viewModel.StatusMessage.Should().Contain(".pdf");
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
            pdf.Setup(p => p.ConvertPdfPageToImageAsync(
                    selectedPath,
                    0,
                    It.IsAny<ResolutionMode>(),
                    It.IsAny<double>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreatePreviewBitmap());

            try
            {
                StaTestHelper.Run(() => viewModel.BrowseCommand.Execute(null));

                viewModel.FilePath.Should().Be(selectedPath);
                dialog.Verify(d => d.ShowOpenDocumentFileDialog(), Times.Once);
            }
            finally
            {
                File.Delete(selectedPath);
            }
        }

        /// <summary>
        /// プレビュー画像がない状態でコピーコマンドを実行した場合にエラーステータスが設定され、クリップボード操作が行われないことを検証する
        /// </summary>
        [Fact]
        public void CopyToClipboardCommand_WithoutPreview_SetsStatusMessage()
        {
            var (viewModel, _, _, clipboard) = MainViewModelTestFactory.Create();
            viewModel.PreviewImage = null;

            StaTestHelper.Run(() => viewModel.CopyToClipboardCommand.Execute(null));

            viewModel.StatusMessage.Should().Contain("ありません");
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
            viewModel.PreviewImage = image;

            StaTestHelper.Run(() => viewModel.CopyToClipboardCommand.Execute(null));

            clipboard.Verify(c => c.CopyImage(image, true), Times.Once);
            viewModel.StatusMessage.Should().Contain("コピーしました");
        }

        /// <summary>
        /// ファイルパス未設定時に SaveCommand が実行不可であることを検証する
        /// </summary>
        [Fact]
        public void SaveCommand_WhenFilePathEmpty_CannotExecute()
        {
            var (viewModel, _, _, _) = MainViewModelTestFactory.Create();
            viewModel.FilePath = null;

            viewModel.SaveCommand.CanExecute(null).Should().BeFalse();
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

            viewModel.SaveCommand.CanExecute(null).Should().BeTrue();
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
            viewModel.BrowseCommand.CanExecute(null).Should().BeFalse();
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
            viewModel.CancelCommand.CanExecute(null).Should().BeTrue();
        }

        /// <summary>
        /// プロパティ変更時に PropertyChanged イベントが発火することを検証する
        /// </summary>
        [Fact]
        public void SetProperty_RaisesPropertyChanged()
        {
            var (viewModel, _, _, _) = MainViewModelTestFactory.Create();
            var raised = false;
            viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(viewModel.StatusMessage))
                {
                    raised = true;
                }
            };

            viewModel.StatusMessage = "updated";

            raised.Should().BeTrue();
            viewModel.StatusMessage.Should().Be("updated");
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
            viewModel.PageCount = 1;
            viewModel.ExportSettings.PageRange = "1";
            viewModel.ExportSettings.ResolutionValue = "1080";
            dialog.Setup(d => d.ShowFolderBrowserDialog()).Returns(folder);
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
            await Task.Delay(300);

            viewModel.StatusMessage.Should().Contain("完了");
            Directory.Delete(folder, recursive: true);
        }

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

            viewModel.LoadPdfFromPathCommand.CanExecute(null).Should().BeTrue();
        }

        /// <summary>
        /// 処理中は LoadPdfFromPathCommand が実行不可であることを検証する
        /// </summary>
        [Fact]
        public void LoadPdfFromPathCommand_WhenBusy_CannotExecute()
        {
            var (viewModel, _, _, _) = MainViewModelTestFactory.Create();
            viewModel.IsBusy = true;

            viewModel.LoadPdfFromPathCommand.CanExecute(null).Should().BeFalse();
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

                viewModel.FilePath.Should().Be(pdfPath);
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

            viewModel.FilePath.Should().Be("C:\\existing.pdf");
        }
    }
}
