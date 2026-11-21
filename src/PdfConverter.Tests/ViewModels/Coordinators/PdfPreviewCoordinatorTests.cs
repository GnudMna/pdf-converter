using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
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
        /// <summary>
        /// ファイルパスが空の場合に何も処理されないことを検証する
        /// </summary>
        [Fact]
        public void LoadFromPath_EmptyPath_DoesNothing()
        {
            var coordinator = CreateCoordinator(out _);
            var host = new TestMainViewModelHost();

            coordinator.LoadFromPath(host);

            host.StatusMessage.Should().BeNull();
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

            host.PageCount.Should().Be(0);
            host.PreviewImage.Should().BeNull();
            host.LoadedFilePath.Should().BeNull();
            host.StatusMessage.Should().Contain("見つかりません");
        }

        /// <summary>
        /// .pdf 以外の拡張子を指定した場合にエラーメッセージが設定されることを検証する
        /// </summary>
        [Fact]
        public void LoadFromPath_NonPdfExtension_SetsErrorStatus()
        {
            var coordinator = CreateCoordinator(out _);
            string path = Path.GetTempFileName();
            var host = new TestMainViewModelHost { FilePath = path };

            try
            {
                coordinator.LoadFromPath(host);

                host.StatusMessage.Should().Contain(".pdf");
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
        /// 保存処理中（IsSaving）のときに新しい読み込みが開始されないことを検証する
        /// </summary>
        [Fact]
        public void LoadFromPath_WhenSaving_DoesNotStartLoad()
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
            pdf.Setup(p => p.ConvertPdfPageToImageAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<Models.ResolutionMode>(),
                    It.IsAny<double>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((System.Windows.Media.Imaging.BitmapSource)null);

            try
            {
                coordinator.LoadFromPath(host, forceReload: true);
                await Task.Delay(50);

                host.IsBusy.Should().BeTrue();

                pageCountGate.SetResult(1);
                await WaitForAsyncOperations();

                host.IsBusy.Should().BeFalse();
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
            pdf.Setup(p => p.ConvertPdfPageToImageAsync(
                    path,
                    0,
                    host.ResolutionMode,
                    1080,
                    true,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(bitmap);

            try
            {
                coordinator.LoadFromPath(host, forceReload: true);
                await WaitForAsyncOperations();

                host.PageCount.Should().Be(3);
                host.PreviewImage.Should().BeSameAs(bitmap);
                host.StatusMessage.Should().Contain("更新");
                host.IsBusy.Should().BeFalse();
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
            pdf.Setup(p => p.ConvertPdfPageToImageAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<Models.ResolutionMode>(),
                    It.IsAny<double>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(previewBitmap);

            try
            {
                await coordinator.GoToPreviousPageAsync(host);

                host.PageNumber.Should().Be("1");
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
            var firstConvertGate = new TaskCompletionSource<System.Windows.Media.Imaging.BitmapSource>();
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

            pdf.Setup(p => p.ConvertPdfPageToImageAsync(
                    path,
                    0,
                    Models.ResolutionMode.Width,
                    1080,
                    true,
                    It.IsAny<CancellationToken>()))
                .Returns(firstConvertGate.Task);
            pdf.Setup(p => p.ConvertPdfPageToImageAsync(
                    path,
                    0,
                    Models.ResolutionMode.Width,
                    800,
                    true,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(secondBitmap);

            try
            {
                coordinator.RequestRefreshIfLoaded(host);
                await Task.Delay(50);
                host.ResolutionValue = "800";
                coordinator.RequestRefreshIfLoaded(host);

                firstConvertGate.SetResult(firstBitmap);
                await WaitForAsyncOperations();

                host.PreviewImage.Should().BeSameAs(secondBitmap);
                host.IsBusy.Should().BeFalse();
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// PDF が未読み込み（PageCount が 0）のときにプレビュー再生成が行われないことを検証する
        /// </summary>
        [Fact]
        public async Task RefreshIfLoadedAsync_WhenNotLoaded_DoesNothing()
        {
            var coordinator = CreateCoordinator(out var pdf);
            var host = new TestMainViewModelHost { PageCount = 0 };

            await coordinator.RefreshIfLoadedAsync(host);

            pdf.Verify(p => p.ConvertPdfPageToImageAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<Models.ResolutionMode>(),
                It.IsAny<double>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        private static PdfPreviewCoordinator CreateCoordinator(out Mock<IPdfConversionService> pdf)
        {
            pdf = new Mock<IPdfConversionService>();
            return new PdfPreviewCoordinator(pdf.Object);
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
