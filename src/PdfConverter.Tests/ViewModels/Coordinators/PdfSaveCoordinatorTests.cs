using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using PdfConverter.Models;
using PdfConverter.Services;
using PdfConverter.Tests.Helpers;
using PdfConverter.ViewModels.Coordinators;
using Xunit;

namespace PdfConverter.Tests.ViewModels.Coordinators
{
    /// <summary>
    /// <see cref="PdfSaveCoordinator"/> の一括保存フローと上書き確認を検証する
    /// </summary>
    public class PdfSaveCoordinatorTests
    {
        /// <summary>
        /// ファイルパスが空の場合に保存処理が開始されないことを検証する
        /// </summary>
        [Fact]
        public async Task SaveAsync_EmptyFilePath_DoesNothing()
        {
            var coordinator = CreateCoordinator(out var pdf, out _);
            var host = new TestMainViewModelHost();

            await coordinator.SaveAsync(host);

            pdf.Verify(p => p.SavePdfPagesToImagesAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<int>>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<ResolutionMode>(),
                It.IsAny<double>(),
                It.IsAny<OutputImageFormat>(),
                It.IsAny<bool>(),
                It.IsAny<IProgress<SaveProgressReport>>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        /// <summary>
        /// フォルダー選択ダイアログがキャンセルされた場合にステータスが更新され、Busy/Saving が false に戻ることを検証する
        /// </summary>
        [Fact]
        public async Task SaveAsync_FolderDialogCancelled_SetsStatusMessage()
        {
            var coordinator = CreateCoordinator(out _, out var dialog);
            var host = new TestMainViewModelHost
            {
                FilePath = "C:\\sample.pdf",
                PageCount = 2,
                ResolutionValue = "1080",
            };
            dialog.Setup(d => d.ShowFolderBrowserDialog()).Returns((string)null);

            await coordinator.SaveAsync(host);

            host.StatusMessage.Should().Contain("キャンセル");
            host.IsBusy.Should().BeFalse();
            host.IsSaving.Should().BeFalse();
        }

        /// <summary>
        /// 不正なページ範囲指定時に検証メッセージが設定され、保存が実行されないことを検証する
        /// </summary>
        [Fact]
        public async Task SaveAsync_InvalidPageRange_SetsValidationAndStops()
        {
            var coordinator = CreateCoordinator(out var pdf, out var dialog);
            string folder = CreateTempDirectory();
            var host = new TestMainViewModelHost
            {
                FilePath = "C:\\sample.pdf",
                PageCount = 2,
                PageRange = "invalid",
                ResolutionValue = "1080",
            };
            dialog.Setup(d => d.ShowFolderBrowserDialog()).Returns(folder);

            await coordinator.SaveAsync(host);

            host.PageRangeValidationMessage.Should().NotBeNullOrWhiteSpace();
            host.StatusKind.Should().Be(StatusKind.Warning);
            pdf.Verify(p => p.SavePdfPagesToImagesAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<int>>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<ResolutionMode>(),
                It.IsAny<double>(),
                It.IsAny<OutputImageFormat>(),
                It.IsAny<bool>(),
                It.IsAny<IProgress<SaveProgressReport>>(),
                It.IsAny<CancellationToken>()), Times.Never);
            Directory.Delete(folder, recursive: true);
        }

        /// <summary>
        /// 既存ファイルの上書き確認で「いいえ」が選択された場合に保存が実行されないことを検証する
        /// </summary>
        [Fact]
        public async Task SaveAsync_OverwriteDeclined_DoesNotSave()
        {
            var coordinator = CreateCoordinator(out var pdf, out var dialog);
            string folder = CreateTempDirectory();
            File.WriteAllText(Path.Combine(folder, "page_1.png"), "existing");
            var host = new TestMainViewModelHost
            {
                FilePath = "C:\\sample.pdf",
                PageCount = 1,
                PageRange = "1",
                ResolutionValue = "1080",
                OutputImageFormat = OutputImageFormat.Png,
            };
            dialog.Setup(d => d.ShowFolderBrowserDialog()).Returns(folder);
            dialog.Setup(d => d.ShowYesNo(It.IsAny<string>(), It.IsAny<string>(), DialogIcon.Warning)).Returns(false);

            await coordinator.SaveAsync(host);

            host.StatusMessage.Should().Contain("キャンセル");
            pdf.Verify(p => p.SavePdfPagesToImagesAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<int>>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<ResolutionMode>(),
                It.IsAny<double>(),
                It.IsAny<OutputImageFormat>(),
                It.IsAny<bool>(),
                It.IsAny<IProgress<SaveProgressReport>>(),
                It.IsAny<CancellationToken>()), Times.Never);
            Directory.Delete(folder, recursive: true);
        }

        /// <summary>
        /// 保存処理中にキャンセルされた場合、完了メッセージを表示せずキャンセル文言を設定することを検証する
        /// </summary>
        [Fact]
        public async Task SaveAsync_CancellationRequested_SetsCancelledStatus()
        {
            var coordinator = CreateCoordinator(out var pdf, out var dialog);
            string folder = CreateTempDirectory();
            var host = new TestMainViewModelHost
            {
                FilePath = "C:\\sample.pdf",
                PageCount = 2,
                PageRange = "1-2",
                ResolutionValue = "1080",
            };
            dialog.Setup(d => d.ShowFolderBrowserDialog()).Returns(folder);
            pdf.Setup(p => p.SavePdfPagesToImagesAsync(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<int>>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<ResolutionMode>(),
                    It.IsAny<double>(),
                    It.IsAny<OutputImageFormat>(),
                    It.IsAny<bool>(),
                    It.IsAny<IProgress<SaveProgressReport>>(),
                    It.IsAny<CancellationToken>()))
                .Callback(() => host.Cancel())
                .Returns(Task.CompletedTask);

            await coordinator.SaveAsync(host);

            host.StatusMessage.Should().Contain("キャンセル");
            host.StatusKind.Should().NotBe(StatusKind.Success);
            Directory.Delete(folder, recursive: true);
        }

        /// <summary>
        /// 正常な保存リクエストでページ保存が実行され、進捗 100% と完了メッセージが設定されることを検証する
        /// </summary>
        [Fact]
        public async Task SaveAsync_ValidRequest_SavesPages()
        {
            var coordinator = CreateCoordinator(out var pdf, out var dialog);
            string folder = CreateTempDirectory();
            var host = new TestMainViewModelHost
            {
                FilePath = "C:\\sample.pdf",
                PageCount = 2,
                PageRange = "1-2",
                ResolutionValue = "1080",
                OutputImageFormat = OutputImageFormat.Png,
            };
            dialog.Setup(d => d.ShowFolderBrowserDialog()).Returns(folder);
            pdf.Setup(p => p.SavePdfPagesToImagesAsync(
                    host.FilePath,
                    It.IsAny<IEnumerable<int>>(),
                    folder,
                    false,
                    host.ResolutionMode,
                    1080,
                    host.OutputImageFormat,
                    true,
                    It.IsAny<IProgress<SaveProgressReport>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await coordinator.SaveAsync(host);

            host.ProgressValue.Should().Be(100);
            host.StatusMessage.Should().Contain("完了");
            host.StatusKind.Should().Be(StatusKind.Success);
            Directory.Delete(folder, recursive: true);
        }

        /// <summary>
        /// PDF ファイル入力時に PDF 保存処理が開始されないことを検証する
        /// </summary>
        [Fact]
        public async Task SavePdfAsync_NonWordFile_DoesNothing()
        {
            var coordinator = CreateCoordinator(out _, out var dialog, out _);
            var host = new TestMainViewModelHost
            {
                FilePath = "C:\\sample.pdf",
                PageCount = 2,
            };

            await coordinator.SavePdfAsync(host);

            dialog.Verify(d => d.ShowSavePdfFileDialog(It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// PDF 保存ダイアログがキャンセルされた場合にステータスが更新され、Busy が false に戻ることを検証する
        /// </summary>
        [Fact]
        public async Task SavePdfAsync_SaveDialogCancelled_SetsStatusMessage()
        {
            var coordinator = CreateCoordinator(out _, out var dialog, out _);
            var host = new TestMainViewModelHost
            {
                FilePath = "C:\\sample.docx",
                PageCount = 2,
            };
            dialog.Setup(d => d.ShowSavePdfFileDialog("sample.pdf")).Returns((string)null);

            await coordinator.SavePdfAsync(host);

            host.StatusMessage.Should().Contain("キャンセル");
            host.IsBusy.Should().BeFalse();
        }

        /// <summary>
        /// 変換済み PDF が指定先にコピーされ、成功ステータスが設定されることを検証する
        /// </summary>
        [Fact]
        public async Task SavePdfAsync_CopiesPdfToDestination_SetsSuccess()
        {
            string tempDir = CreateTempDirectory();
            string sourcePdf = Path.Combine(tempDir, "source.pdf");
            File.WriteAllText(sourcePdf, "pdf-content");
            string destinationPdf = Path.Combine(tempDir, "output.pdf");

            var coordinator = CreateCoordinator(out _, out var dialog, out var documentPdfSource);
            documentPdfSource.Setup(d => d.GetPdfPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sourcePdf);
            dialog.Setup(d => d.ShowSavePdfFileDialog("sample.pdf")).Returns(destinationPdf);

            var host = new TestMainViewModelHost
            {
                FilePath = "C:\\sample.docx",
                PageCount = 1,
            };

            await coordinator.SavePdfAsync(host);

            File.ReadAllText(destinationPdf).Should().Be("pdf-content");
            host.StatusKind.Should().Be(StatusKind.Success);
            host.StatusMessage.Should().Contain("PDFを保存しました");
            host.IsBusy.Should().BeFalse();
            Directory.Delete(tempDir, recursive: true);
        }

        /// <summary>
        /// 上書き確認でキャンセルされた場合にファイルが更新されないことを検証する
        /// </summary>
        [Fact]
        public async Task SavePdfAsync_OverwriteDeclined_DoesNotCopy()
        {
            string tempDir = CreateTempDirectory();
            string sourcePdf = Path.Combine(tempDir, "source.pdf");
            File.WriteAllText(sourcePdf, "new-content");
            string destinationPdf = Path.Combine(tempDir, "output.pdf");
            File.WriteAllText(destinationPdf, "existing-content");

            var coordinator = CreateCoordinator(out _, out var dialog, out var documentPdfSource);
            documentPdfSource.Setup(d => d.GetPdfPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sourcePdf);
            dialog.Setup(d => d.ShowSavePdfFileDialog("sample.pdf")).Returns(destinationPdf);
            dialog.Setup(d => d.ShowYesNo(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<DialogIcon>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .Returns(false);

            var host = new TestMainViewModelHost
            {
                FilePath = "C:\\sample.docx",
                PageCount = 1,
            };

            await coordinator.SavePdfAsync(host);

            File.ReadAllText(destinationPdf).Should().Be("existing-content");
            host.StatusMessage.Should().Contain("キャンセル");
            host.IsBusy.Should().BeFalse();
            Directory.Delete(tempDir, recursive: true);
        }

        private static PdfSaveCoordinator CreateCoordinator(out Mock<IPdfConversionService> pdf, out Mock<IDialogService> dialog)
        {
            return CreateCoordinator(out pdf, out dialog, out _);
        }

        private static PdfSaveCoordinator CreateCoordinator(
            out Mock<IPdfConversionService> pdf,
            out Mock<IDialogService> dialog,
            out Mock<IDocumentPdfSourceService> documentPdfSource)
        {
            pdf = new Mock<IPdfConversionService>();
            dialog = new Mock<IDialogService>();
            documentPdfSource = DocumentPdfSourceTestHelper.CreatePassthrough();
            return new PdfSaveCoordinator(pdf.Object, documentPdfSource.Object, dialog.Object);
        }

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
