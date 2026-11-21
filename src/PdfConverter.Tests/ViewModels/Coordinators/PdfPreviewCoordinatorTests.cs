using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using PdfConverter.Models;
using PdfConverter.Services;
using PdfConverter.Tests.Helpers;
using PdfConverter.ViewModels.Coordinators;
using Xunit;

namespace PdfConverter.Tests.ViewModels.Coordinators
{
    /// <summary>
    /// <see cref="PdfPreviewCoordinator"/> の PDF 読み込み・プレビュー生成・ページ移動を検証する
    /// </summary>
    public class PdfPreviewCoordinatorTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// ファイルパスが空の場合に何も処理されないことを検証する
        /// </summary>
        [Fact]
        public void LoadFromPath_EmptyPath_DoesNothing()
        {
            var coordinator = CreateCoordinator(out _);
            var host = new TestMainViewModelHost();

            coordinator.LoadFromPath(host);

            Assert.Null(host.StatusMessage);
        }

        /// <summary>
        /// 存在しないファイルを指定した場合にエラーステータスが設定され、状態がリセットされることを検証する
        /// </summary>
        [Fact]
        public void LoadFromPath_MissingFile_SetsErrorStatus()
        {
            var coordinator = CreateCoordinator(out _);
            var host = new TestMainViewModelHost
            {
                FilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf"),
            };

            coordinator.LoadFromPath(host);

            Assert.Equal(0, host.PageCount);
            Assert.Null(host.PreviewImage);
            Assert.Null(host.LoadedFilePath);
            Assert.Contains("見つかりません", host.StatusMessage);
        }

        /// <summary>
        /// サポート対象外の拡張子を指定した場合にエラーメッセージが設定されることを検証する
        /// </summary>
        [Fact]
        public void LoadFromPath_UnsupportedExtension_SetsErrorStatus()
        {
            var coordinator = CreateCoordinator(out _);
            string path = Path.GetTempFileName();
            var host = new TestMainViewModelHost { FilePath = path };

            try
            {
                coordinator.LoadFromPath(host);

                Assert.Contains("Word", host.StatusMessage);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// 同一パスで forceReload が false の場合に再読み込みが行われないことを検証する
        /// </summary>
        [Fact]
        public void LoadFromPath_SamePathWithoutForce_DoesNotReload()
        {
            var coordinator = CreateCoordinator(out var pdf);
            var host = new TestMainViewModelHost
            {
                FilePath = CreateTempPdfPath(),
                LoadedFilePath = null,
            };
            host.LoadedFilePath = host.FilePath;

            coordinator.LoadFromPath(host, forceReload: false);

            pdf.Verify(p => p.GetPdfPageCountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            File.Delete(host.FilePath);
        }

        /// <summary>
        /// 保存処理中 (IsSaving) のときに新しい読み込みが開始されず、読み込めない旨を警告表示することを検証する
        /// </summary>
        [Fact]
        public void LoadFromPath_WhenSaving_DoesNotStartLoadAndShowsWarning()
        {
            var coordinator = CreateCoordinator(out var pdf);
            var host = new TestMainViewModelHost
            {
                FilePath = CreateTempPdfPath(),
                IsSaving = true,
            };

            try
            {
                coordinator.LoadFromPath(host, forceReload: true);

                pdf.Verify(p => p.GetPdfPageCountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
                Assert.Equal(DocumentFileHelper.BusyLoadRejectedMessage, host.StatusMessage);
                Assert.Equal(StatusKind.Warning, host.StatusKind);
            }
            finally
            {
                File.Delete(host.FilePath);
            }
        }

        /// <summary>
        /// ページ数取得中に IsBusy が true になることを検証する
        /// </summary>
        [Fact]
        public async Task LoadFromPath_DuringPageCountFetch_IsBusy()
        {
            var coordinator = CreateCoordinator(out var pdf);
            string path = CreateTempPdfPath();
            var pageCountGate = new TaskCompletionSource<int>();
            var host = new TestMainViewModelHost
            {
                FilePath = path,
                PageNumber = "1",
                ResolutionValue = "1080",
            };

            pdf.Setup(p => p.GetPdfPageCountAsync(path, It.IsAny<CancellationToken>())).Returns(pageCountGate.Task);
            pdf.Setup(p => p.RenderPdfPagePreviewAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<Models.ResolutionMode>(),
                    It.IsAny<double>(),
                    It.IsAny<bool>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((PdfPagePreview)null);

            try
            {
                coordinator.LoadFromPath(host, forceReload: true);
                await Task.Delay(50, cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(host.IsBusy);

                pageCountGate.SetResult(1);
                await WaitForAsyncOperations();

                Assert.False(host.IsBusy);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// 有効な PDF パスでページ数取得とプレビュー生成が完了し、IsBusy が false に戻ることを検証する
        /// </summary>
        [Fact]
        public async Task LoadFromPath_ValidPdf_LoadsPageCountAndPreview()
        {
            var coordinator = CreateCoordinator(out var pdf);
            string path = CreateTempPdfPath();
            System.Windows.Media.Imaging.BitmapSource bitmap = null;
            StaTestHelper.Run(() => bitmap = BitmapTestHelper.CreateBitmap());
            var host = new TestMainViewModelHost
            {
                FilePath = path,
                PageNumber = "1",
                ResolutionMode = Models.ResolutionMode.Width,
                ResolutionValue = "1080",
            };

            pdf.Setup(p => p.GetPdfPageCountAsync(path, It.IsAny<CancellationToken>())).ReturnsAsync(3);
            pdf.Setup(p => p.RenderPdfPagePreviewAsync(
                    path,
                    0,
                    host.ResolutionMode,
                    1080,
                    true,
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreatePreview(bitmap));

            try
            {
                coordinator.LoadFromPath(host, forceReload: true);
                await WaitForAsyncOperations();

                Assert.Equal(3, host.PageCount);
                Assert.Same(bitmap, host.PreviewImage);
                Assert.Contains("更新", host.StatusMessage);
                Assert.False(host.IsBusy);
            }
            finally
            {
                File.Delete(path);
            }

        }

        /// <summary>
        /// 2 ページ目から前ページへ移動したときに PageNumber が 1 に減算されることを検証する
        /// </summary>
        [Fact]
        public async Task GoToPreviousPageAsync_OnSecondPage_DecrementsPageNumber()
        {
            var coordinator = CreateCoordinator(out var pdf);
            var host = new TestMainViewModelHost
            {
                FilePath = CreateTempPdfPath(),
                PageCount = 3,
                PageNumber = "2",
                ResolutionValue = "1080",
            };
            System.Windows.Media.Imaging.BitmapSource previewBitmap = null;
            StaTestHelper.Run(() => previewBitmap = BitmapTestHelper.CreateBitmap());
            pdf.Setup(p => p.RenderPdfPagePreviewAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<Models.ResolutionMode>(),
                    It.IsAny<double>(),
                    It.IsAny<bool>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreatePreview(previewBitmap));

            try
            {
                await coordinator.GoToPreviousPageAsync(host);

                Assert.Equal("1", host.PageNumber);
            }
            finally
            {
                File.Delete(host.FilePath);
            }
        }

        /// <summary>
        /// プレビュー切り替えが短時間で完了した場合は処理中 UI を出さないことを検証する
        /// </summary>
        [Fact]
        public async Task GoToNextPageAsync_WhenPreviewCompletesQuickly_DoesNotShowBusyUi()
        {
            var coordinator = CreateCoordinator(out var pdf);
            var host = new TestMainViewModelHost
            {
                FilePath = CreateTempPdfPath(),
                PageCount = 3,
                PageNumber = "1",
                ResolutionValue = "1080",
                StatusMessage = "初期状態",
                StatusKind = StatusKind.Info,
            };
            System.Windows.Media.Imaging.BitmapSource previewBitmap = null;
            StaTestHelper.Run(() => previewBitmap = BitmapTestHelper.CreateBitmap());
            pdf.Setup(p => p.RenderPdfPagePreviewAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<Models.ResolutionMode>(),
                    It.IsAny<double>(),
                    It.IsAny<bool>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreatePreview(previewBitmap));

            try
            {
                await coordinator.GoToNextPageAsync(host);

                Assert.Equal("2", host.PageNumber);
                Assert.False(host.IsBusy);
                Assert.Equal("初期状態", host.StatusMessage);
                Assert.Equal(StatusKind.Info, host.StatusKind);
            }
            finally
            {
                File.Delete(host.FilePath);
            }
        }

        /// <summary>
        /// プレビュー切り替えが遅い場合のみ処理中 UI を出すことを検証する
        /// </summary>
        [Fact]
        public async Task GoToNextPageAsync_WhenPreviewIsSlow_ActivatesBusyUi()
        {
            var coordinator = CreateCoordinator(out var pdf);
            var host = new TestMainViewModelHost
            {
                FilePath = CreateTempPdfPath(),
                PageCount = 3,
                PageNumber = "1",
                ResolutionValue = "1080",
            };
            var convertGate = new TaskCompletionSource<PdfPagePreview>();
            System.Windows.Media.Imaging.BitmapSource previewBitmap = null;
            StaTestHelper.Run(() => previewBitmap = BitmapTestHelper.CreateBitmap());
            pdf.Setup(p => p.RenderPdfPagePreviewAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<Models.ResolutionMode>(),
                    It.IsAny<double>(),
                    It.IsAny<bool>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .Returns(convertGate.Task);

            try
            {
                Task navigationTask = coordinator.GoToNextPageAsync(host);
                await Task.Delay(PreviewUiFeedback.ActivationDelayMilliseconds + 100, cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(host.IsBusy);
                Assert.Equal(StatusKind.Progress, host.StatusKind);

                convertGate.SetResult(CreatePreview(previewBitmap));
                await navigationTask;

                Assert.False(host.IsBusy);
                Assert.Contains("更新", host.StatusMessage);
            }
            finally
            {
                File.Delete(host.FilePath);
            }
        }

        /// <summary>
        /// 連続したプレビュー再生成では最後の解像度設定だけが反映されることを検証する
        /// </summary>
        [Fact]
        public async Task RefreshIfLoadedAsync_RapidRequests_AppliesLatestResolutionOnly()
        {
            var coordinator = CreateCoordinator(out var pdf);
            string path = CreateTempPdfPath();
            var firstConvertGate = new TaskCompletionSource<PdfPagePreview>();
            System.Windows.Media.Imaging.BitmapSource firstBitmap = null;
            System.Windows.Media.Imaging.BitmapSource secondBitmap = null;
            StaTestHelper.Run(() =>
            {
                firstBitmap = BitmapTestHelper.CreateBitmap();
                secondBitmap = BitmapTestHelper.CreateBitmap();
            });
            var host = new TestMainViewModelHost
            {
                FilePath = path,
                PageCount = 1,
                PageNumber = "1",
                ResolutionMode = Models.ResolutionMode.Width,
                ResolutionValue = "1080",
            };

            pdf.Setup(p => p.RenderPdfPagePreviewAsync(
                    path,
                    0,
                    Models.ResolutionMode.Width,
                    1080,
                    true,
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .Returns(firstConvertGate.Task);
            pdf.Setup(p => p.RenderPdfPagePreviewAsync(
                    path,
                    0,
                    Models.ResolutionMode.Width,
                    800,
                    true,
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreatePreview(secondBitmap));

            try
            {
                coordinator.RequestRefreshIfLoaded(host);
                await Task.Delay(50, cancellationToken: TestContext.Current.CancellationToken);
                host.ResolutionValue = "800";
                coordinator.RequestRefreshIfLoaded(host);

                firstConvertGate.SetResult(CreatePreview(firstBitmap));
                await WaitForAsyncOperations();

                Assert.Same(secondBitmap, host.PreviewImage);
                Assert.False(host.IsBusy);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// PDF が未読み込み (PageCount が 0) のときにプレビュー再生成が行われないことを検証する
        /// </summary>
        [Fact]
        public async Task RefreshIfLoadedAsync_WhenNotLoaded_DoesNothing()
        {
            var coordinator = CreateCoordinator(out var pdf);
            var host = new TestMainViewModelHost { PageCount = 0 };

            await coordinator.RefreshIfLoadedAsync(host);

            pdf.Verify(p => p.RenderPdfPagePreviewAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<Models.ResolutionMode>(),
                It.IsAny<double>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        /// <summary>
        /// 読み込み中に出力設定を変えても読み込みが取り消されず、新しいファイルのページ数が反映され、変更後の設定で描画されることを検証する
        /// </summary>
        [Fact]
        public async Task RefreshIfLoadedAsync_DuringLoad_KeepsLoadAndAppliesNewSettingsAfterLoad()
        {
            var coordinator = CreateCoordinator(out var pdf);
            string path = CreateTempPdfPath();
            var pageCountGate = new TaskCompletionSource<int>();
            System.Windows.Media.Imaging.BitmapSource bitmap = null;
            StaTestHelper.Run(() => bitmap = BitmapTestHelper.CreateBitmap());
            var host = new TestMainViewModelHost
            {
                FilePath = path,
                PageCount = 3,
                PageNumber = "1",
                ResolutionMode = Models.ResolutionMode.Width,
                ResolutionValue = "1080",
            };

            pdf.Setup(p => p.GetPdfPageCountAsync(path, It.IsAny<CancellationToken>())).Returns(pageCountGate.Task);
            SetupPreview(pdf, CreatePreview(bitmap));

            try
            {
                coordinator.LoadFromPath(host, forceReload: true);
                await Task.Delay(50, cancellationToken: TestContext.Current.CancellationToken);

                host.ResolutionValue = "800";
                await coordinator.RefreshIfLoadedAsync(host);

                Assert.Equal(0, host.PageCount);
                Assert.True(host.IsBusy);

                pageCountGate.SetResult(5);
                await WaitForAsyncOperations();

                Assert.Equal(5, host.PageCount);
                Assert.Same(bitmap, host.PreviewImage);
                Assert.False(host.IsBusy);
                pdf.Verify(p => p.RenderPdfPagePreviewAsync(
                    path,
                    0,
                    Models.ResolutionMode.Width,
                    800,
                    true,
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// 保存中に出力設定を変えても再描画で保存を取り消さず、保存の完了後に 1 回だけ再描画することを検証する
        /// </summary>
        [Fact]
        public async Task RefreshIfLoadedAsync_WhileSaving_DefersRefreshUntilResumed()
        {
            var coordinator = CreateCoordinator(out var pdf);
            var host = CreateLoadedHost();
            System.Windows.Media.Imaging.BitmapSource bitmap = null;
            StaTestHelper.Run(() => bitmap = BitmapTestHelper.CreateBitmap());
            SetupPreview(pdf, CreatePreview(bitmap));

            try
            {
                host.IsSaving = true;
                await coordinator.RefreshIfLoadedAsync(host);
                await coordinator.RefreshIfLoadedAsync(host);
                Assert.Null(host.PreviewImage);

                host.IsSaving = false;
                coordinator.ResumeDeferredRefresh(host);
                coordinator.ResumeDeferredRefresh(host);
                await WaitForAsyncOperations();

                Assert.Same(bitmap, host.PreviewImage);
                pdf.Verify(p => p.RenderPdfPagePreviewAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<Models.ResolutionMode>(),
                    It.IsAny<double>(),
                    It.IsAny<bool>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()), Times.Once);
            }
            finally
            {
                File.Delete(host.FilePath);
            }
        }

        /// <summary>
        /// プレビューを表示領域のサイズを上限として描画することを検証する
        /// </summary>
        [Fact]
        public async Task GoToPageAsync_WithViewportSize_RendersPreviewWithinViewport()
        {
            var coordinator = CreateCoordinator(out var pdf);
            var host = CreateLoadedHost();
            host.PreviewViewportPixelWidth = 640;
            host.PreviewViewportPixelHeight = 480;
            System.Windows.Media.Imaging.BitmapSource bitmap = null;
            StaTestHelper.Run(() => bitmap = BitmapTestHelper.CreateBitmap());
            SetupPreview(pdf, CreatePreview(bitmap));

            try
            {
                await coordinator.GoToPageAsync(host);

                VerifyPreviewRendered(pdf, 640, 480, Times.Once());
                Assert.Same(bitmap, host.PreviewImage);
            }
            finally
            {
                File.Delete(host.FilePath);
            }
        }

        /// <summary>
        /// 表示領域のサイズが未確定の場合は既定の最大サイズでプレビューを描画することを検証する
        /// </summary>
        [Fact]
        public async Task GoToPageAsync_ViewportNotMeasured_RendersPreviewWithinFallbackSize()
        {
            var coordinator = CreateCoordinator(out var pdf);
            var host = CreateLoadedHost();
            System.Windows.Media.Imaging.BitmapSource bitmap = null;
            StaTestHelper.Run(() => bitmap = BitmapTestHelper.CreateBitmap());
            SetupPreview(pdf, CreatePreview(bitmap));

            try
            {
                await coordinator.GoToPageAsync(host);

                VerifyPreviewRendered(
                    pdf,
                    PdfPreviewCoordinator.FallbackPreviewMaxPixelSize,
                    PdfPreviewCoordinator.FallbackPreviewMaxPixelSize,
                    Times.Once());
            }
            finally
            {
                File.Delete(host.FilePath);
            }
        }

        /// <summary>
        /// 総ページ数を超えるページ番号は描画せず、入力欄の検証メッセージを表示することを検証する
        /// </summary>
        [Fact]
        public async Task GoToPageAsync_PageNumberExceedsPageCount_ShowsValidationMessageWithoutRendering()
        {
            var coordinator = CreateCoordinator(out var pdf);
            var host = CreateLoadedHost();
            host.PageCount = 3;
            host.PageNumber = "4";

            try
            {
                await coordinator.GoToPageAsync(host);

                Assert.Equal("1 ～ 3 のページ番号を入力してください。", host.PageNumberValidationMessage);
                Assert.Equal(StatusKind.Warning, host.StatusKind);
                Assert.Null(host.PreviewImage);
                VerifyPreviewRendered(pdf, PdfPreviewCoordinator.FallbackPreviewMaxPixelSize, PdfPreviewCoordinator.FallbackPreviewMaxPixelSize, Times.Never());
                Assert.False(host.IsBusy);
            }
            finally
            {
                File.Delete(host.FilePath);
            }
        }

        /// <summary>
        /// 総ページ数ちょうどのページ番号は検証メッセージを表示せず描画することを検証する
        /// </summary>
        [Fact]
        public async Task GoToPageAsync_PageNumberEqualsPageCount_RendersLastPage()
        {
            var coordinator = CreateCoordinator(out var pdf);
            var host = CreateLoadedHost();
            host.PageCount = 3;
            host.PageNumber = "3";
            System.Windows.Media.Imaging.BitmapSource bitmap = null;
            StaTestHelper.Run(() => bitmap = BitmapTestHelper.CreateBitmap());
            SetupPreview(pdf, CreatePreview(bitmap));

            try
            {
                await coordinator.GoToPageAsync(host);

                Assert.Null(host.PageNumberValidationMessage);
                Assert.Same(bitmap, host.PreviewImage);
            }
            finally
            {
                File.Delete(host.FilePath);
            }
        }

        /// <summary>
        /// Enter 前のページ番号入力があっても、再生成は描画済みのページを対象にすることを検証する
        /// </summary>
        [Fact]
        public async Task RefreshIfLoadedAsync_PageNumberEditedWithoutEnter_RendersDisplayedPage()
        {
            var coordinator = CreateCoordinator(out var pdf);
            var host = CreateLoadedHost();
            host.PageCount = 10;
            host.PageNumber = "2";
            System.Windows.Media.Imaging.BitmapSource bitmap = null;
            StaTestHelper.Run(() => bitmap = BitmapTestHelper.CreateBitmap());
            SetupPreview(pdf, CreatePreview(bitmap));

            try
            {
                await coordinator.GoToPageAsync(host);
                host.PageNumber = "8";
                await coordinator.RefreshIfLoadedAsync(host);

                Assert.Equal(2, host.DisplayedPageNumber);
                pdf.Verify(p => p.RenderPdfPagePreviewAsync(
                    It.IsAny<string>(),
                    7,
                    It.IsAny<Models.ResolutionMode>(),
                    It.IsAny<double>(),
                    It.IsAny<bool>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()), Times.Never);
            }
            finally
            {
                File.Delete(host.FilePath);
            }
        }

        /// <summary>
        /// 入力欄を空にしたままでも、再生成は描画済みのページで成功することを検証する
        /// </summary>
        [Fact]
        public async Task RefreshIfLoadedAsync_PageNumberCleared_KeepsRenderingDisplayedPage()
        {
            var coordinator = CreateCoordinator(out var pdf);
            var host = CreateLoadedHost();
            host.PageCount = 10;
            host.PageNumber = "2";
            System.Windows.Media.Imaging.BitmapSource bitmap = null;
            StaTestHelper.Run(() => bitmap = BitmapTestHelper.CreateBitmap());
            SetupPreview(pdf, CreatePreview(bitmap));

            try
            {
                await coordinator.GoToPageAsync(host);
                host.PageNumber = string.Empty;
                await coordinator.RefreshIfLoadedAsync(host);

                Assert.NotEqual(StatusKind.Warning, host.StatusKind);
                Assert.Equal(2, host.DisplayedPageNumber);
            }
            finally
            {
                File.Delete(host.FilePath);
            }
        }

        /// <summary>
        /// 縮小描画したプレビューの出力解像度の画像は、同じページ・設定で出力解像度のまま描画し直して返すことを検証する
        /// </summary>
        [Fact]
        public async Task GetOutputResolutionPreviewImageAsync_DownscaledPreview_RendersAtOutputResolution()
        {
            var coordinator = CreateCoordinator(out var pdf);
            var host = CreateLoadedHost();
            System.Windows.Media.Imaging.BitmapSource previewBitmap = null;
            System.Windows.Media.Imaging.BitmapSource outputBitmap = null;
            StaTestHelper.Run(() =>
            {
                previewBitmap = BitmapTestHelper.CreateBitmap();
                outputBitmap = BitmapTestHelper.CreateBitmap(32, 32);
            });
            SetupPreview(pdf, new PdfPagePreview(previewBitmap, 32, 32));
            pdf.Setup(p => p.ConvertPdfPageToImageAsync(
                    host.FilePath,
                    0,
                    Models.ResolutionMode.Width,
                    1080,
                    true,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(outputBitmap);

            try
            {
                await coordinator.GoToPageAsync(host);
                System.Windows.Media.Imaging.BitmapSource image = await coordinator.GetOutputResolutionPreviewImageAsync(host);

                Assert.Same(outputBitmap, image);
                Assert.Same(previewBitmap, host.PreviewImage);
                Assert.False(host.IsBusy);
            }
            finally
            {
                File.Delete(host.FilePath);
            }
        }

        /// <summary>
        /// 出力解像度のまま描画したプレビューは、描画し直さずに表示中の画像を返すことを検証する
        /// </summary>
        [Fact]
        public async Task GetOutputResolutionPreviewImageAsync_PreviewAtOutputResolution_ReturnsDisplayedImage()
        {
            var coordinator = CreateCoordinator(out var pdf);
            var host = CreateLoadedHost();
            System.Windows.Media.Imaging.BitmapSource bitmap = null;
            StaTestHelper.Run(() => bitmap = BitmapTestHelper.CreateBitmap());
            SetupPreview(pdf, CreatePreview(bitmap));

            try
            {
                await coordinator.GoToPageAsync(host);
                System.Windows.Media.Imaging.BitmapSource image = await coordinator.GetOutputResolutionPreviewImageAsync(host);

                Assert.Same(bitmap, image);
                pdf.Verify(p => p.ConvertPdfPageToImageAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<Models.ResolutionMode>(),
                    It.IsAny<double>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()), Times.Never);
            }
            finally
            {
                File.Delete(host.FilePath);
            }
        }

        /// <summary>
        /// 縮小描画したプレビューの表示領域が広がった場合は、少し待ってから新しいサイズで描画し直すことを検証する
        /// </summary>
        [Fact]
        public async Task NotifyPreviewViewportChanged_ViewportGrowsWithDownscaledPreview_RerendersPreview()
        {
            var coordinator = CreateCoordinator(out var pdf);
            var host = CreateLoadedHost();
            host.PreviewViewportPixelWidth = 8;
            host.PreviewViewportPixelHeight = 8;
            System.Windows.Media.Imaging.BitmapSource bitmap = null;
            StaTestHelper.Run(() => bitmap = BitmapTestHelper.CreateBitmap());
            SetupPreview(pdf, new PdfPagePreview(bitmap, 100, 100));

            try
            {
                await coordinator.GoToPageAsync(host);
                host.PreviewViewportPixelWidth = 64;
                host.PreviewViewportPixelHeight = 64;
                coordinator.NotifyPreviewViewportChanged(host);

                VerifyPreviewRendered(pdf, 64, 64, Times.Never());
                await Task.Delay(PdfPreviewCoordinator.ViewportRefreshDelayMilliseconds + 300, cancellationToken: TestContext.Current.CancellationToken);

                VerifyPreviewRendered(pdf, 64, 64, Times.Once());
            }
            finally
            {
                File.Delete(host.FilePath);
            }
        }

        /// <summary>
        /// 出力解像度のまま描画したプレビューは、表示領域が広がっても描画し直さないことを検証する
        /// </summary>
        [Fact]
        public async Task NotifyPreviewViewportChanged_PreviewAtOutputResolution_DoesNotRerender()
        {
            var coordinator = CreateCoordinator(out var pdf);
            var host = CreateLoadedHost();
            host.PreviewViewportPixelWidth = 8;
            host.PreviewViewportPixelHeight = 8;
            System.Windows.Media.Imaging.BitmapSource bitmap = null;
            StaTestHelper.Run(() => bitmap = BitmapTestHelper.CreateBitmap());
            SetupPreview(pdf, CreatePreview(bitmap));

            try
            {
                await coordinator.GoToPageAsync(host);
                host.PreviewViewportPixelWidth = 64;
                host.PreviewViewportPixelHeight = 64;
                coordinator.NotifyPreviewViewportChanged(host);
                await Task.Delay(PdfPreviewCoordinator.ViewportRefreshDelayMilliseconds + 300, cancellationToken: TestContext.Current.CancellationToken);

                VerifyPreviewRendered(pdf, 64, 64, Times.Never());
            }
            finally
            {
                File.Delete(host.FilePath);
            }
        }


        /// <summary>
        /// パスワードが必要な PDF では入力を求め、入力されたパスワードを設定して読み込み直すことを検証する
        /// </summary>
        [Fact]
        public async Task LoadFromPath_PasswordRequired_PromptsAndRetriesWithPassword()
        {
            var coordinator = CreateCoordinator(out var pdf);
            string path = CreateTempPdfPath();
            var host = new TestMainViewModelHost
            {
                FilePath = path,
                PageNumber = "1",
                ResolutionValue = "1080",
                PasswordResponse = () => "secret",
            };
            pdf.SetupSequence(p => p.GetPdfPageCountAsync(path, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new PdfPasswordRequiredException(wasPasswordProvided: false))
                .ReturnsAsync(3);

            try
            {
                coordinator.LoadFromPath(host, forceReload: true);
                await WaitForAsyncOperations();

                Assert.Equal(new[] { false }, host.PasswordRequests);
                pdf.Verify(p => p.SetPdfPassword(path, "secret"), Times.Once);
                Assert.Equal(3, host.PageCount);
                Assert.False(host.IsBusy);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// 入力されたパスワードが正しくない場合は、正しくなかった旨を伝えて再度入力を求めることを検証する
        /// </summary>
        [Fact]
        public async Task LoadFromPath_PasswordIncorrect_PromptsAgainAsIncorrect()
        {
            var coordinator = CreateCoordinator(out var pdf);
            string path = CreateTempPdfPath();
            var host = new TestMainViewModelHost
            {
                FilePath = path,
                PageNumber = "1",
                ResolutionValue = "1080",
                PasswordResponse = () => "secret",
            };
            pdf.SetupSequence(p => p.GetPdfPageCountAsync(path, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new PdfPasswordRequiredException(wasPasswordProvided: false))
                .ThrowsAsync(new PdfPasswordRequiredException(wasPasswordProvided: true))
                .ReturnsAsync(2);

            try
            {
                coordinator.LoadFromPath(host, forceReload: true);
                await WaitForAsyncOperations();

                Assert.Equal(new[] { false, true }, host.PasswordRequests);
                Assert.Equal(2, host.PageCount);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// パスワードの入力がキャンセルされた場合は、読み込みを中止して状態をリセットし、エラーを表示することを検証する
        /// </summary>
        [Fact]
        public async Task LoadFromPath_PasswordPromptCanceled_ResetsStateAndShowsError()
        {
            var coordinator = CreateCoordinator(out var pdf);
            string path = CreateTempPdfPath();
            var host = new TestMainViewModelHost
            {
                FilePath = path,
                PageNumber = "1",
                ResolutionValue = "1080",
                PasswordResponse = () => null,
            };
            pdf.Setup(p => p.GetPdfPageCountAsync(path, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new PdfPasswordRequiredException(wasPasswordProvided: false));

            try
            {
                coordinator.LoadFromPath(host, forceReload: true);
                await WaitForAsyncOperations();

                Assert.Single(host.PasswordRequests);
                pdf.Verify(p => p.SetPdfPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
                Assert.Equal(0, host.PageCount);
                Assert.Null(host.LoadedFilePath);
                Assert.Equal(StatusKind.Error, host.StatusKind);
                Assert.Contains("パスワード", host.StatusMessage);
                Assert.False(host.IsBusy);
            }
            finally
            {
                File.Delete(path);
            }
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/

        private static PdfPagePreview CreatePreview(System.Windows.Media.Imaging.BitmapSource bitmap)
        {
            return new PdfPagePreview(bitmap, bitmap.PixelWidth, bitmap.PixelHeight);
        }

        private static TestMainViewModelHost CreateLoadedHost()
        {
            return new TestMainViewModelHost
            {
                FilePath = CreateTempPdfPath(),
                PageCount = 1,
                PageNumber = "1",
                ResolutionMode = Models.ResolutionMode.Width,
                ResolutionValue = "1080",
            };
        }

        private static void SetupPreview(Mock<IPdfConversionService> pdf, PdfPagePreview preview)
        {
            pdf.Setup(p => p.RenderPdfPagePreviewAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<Models.ResolutionMode>(),
                    It.IsAny<double>(),
                    It.IsAny<bool>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(preview);
        }

        private static void VerifyPreviewRendered(Mock<IPdfConversionService> pdf, int maxPixelWidth, int maxPixelHeight, Times times)
        {
            pdf.Verify(p => p.RenderPdfPagePreviewAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<Models.ResolutionMode>(),
                It.IsAny<double>(),
                It.IsAny<bool>(),
                maxPixelWidth,
                maxPixelHeight,
                It.IsAny<CancellationToken>()), times);
        }

        private static PdfPreviewCoordinator CreateCoordinator(out Mock<IPdfConversionService> pdf)
        {
            pdf = new Mock<IPdfConversionService>();
            var documentPdfSource = DocumentPdfSourceTestHelper.CreatePassthrough();
            return new PdfPreviewCoordinator(pdf.Object, documentPdfSource.Object);
        }

        private static string CreateTempPdfPath()
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}.pdf");
            File.WriteAllText(path, "placeholder");
            return path;
        }

        private static async Task WaitForAsyncOperations()
        {
            for (int i = 0; i < 50; i++)
            {
                await Task.Delay(20);
            }
        }
    }
}
