using Moq;
using PdfConverter.Tests.Helpers;
using PdfConverter.ViewModels;
using PdfConverter.ViewModels.Coordinators;
using Xunit;

namespace PdfConverter.Tests.ViewModels
{
    /// <summary>
    /// <see cref="PreviewPanelViewModel"/> のプレビュー表示・ページナビゲーションを検証する
    /// </summary>
    public class PreviewPanelViewModelTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// ページ数が 0 のときはページ移動 UI を非表示にすることを検証する
        /// </summary>
        [Fact]
        public void IsPageNavigationVisible_WhenNoPages_IsFalse()
        {
            var viewModel = CreateViewModel();

            Assert.False(viewModel.IsPageNavigationVisible);

            viewModel.PageCount = 3;

            Assert.True(viewModel.IsPageNavigationVisible);
        }

        /// <summary>
        /// ファイルパス未設定時に PageIndicator が選択を促すメッセージを返すことを検証する
        /// </summary>
        [Fact]
        public void PageIndicator_WhenNoFilePath_ShowsPrompt()
        {
            var host = CreateHostMock();
            host.SetupGet(h => h.FilePath).Returns((string)null);
            var viewModel = CreateViewModel(host.Object);

            Assert.Equal("ファイルを選択してください", viewModel.PageIndicator);
        }

        /// <summary>
        /// ページ番号変更時に PageIndicator が更新されることを検証する
        /// </summary>
        [Fact]
        public void PageIndicator_WhenLoaded_ShowsCurrentPage()
        {
            var host = CreateHostMock();
            host.SetupGet(h => h.FilePath).Returns("C:\\sample.pdf");
            var viewModel = CreateViewModel(host.Object);
            viewModel.PageCount = 5;
            viewModel.PageNumber = "3";

            Assert.Equal("3/5 ページ", viewModel.PageIndicator);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        private static PreviewPanelViewModel CreateViewModel(IPreviewCoordinatorHost host = null)
        {
            var previewCoordinator = new Mock<IPdfPreviewCoordinator>();
            host = host ?? CreateHostMock().Object;
            PreviewPanelViewModel viewModel = null;
            StaTestHelper.Run(() => viewModel = new PreviewPanelViewModel(
                previewCoordinator.Object,
                () => host,
                () => false,
                _ => { }));
            return viewModel;
        }

        private static Mock<IPreviewCoordinatorHost> CreateHostMock()
        {
            var host = new Mock<IPreviewCoordinatorHost>();
            host.SetupGet(h => h.FilePath).Returns(string.Empty);
            return host;
        }
    }
}
