using System.Windows;
using System.Windows.Input;
using Moq;
using PdfConverter.Tests.Helpers;
using PdfConverter.ViewModels;
using PdfConverter.Views.Behaviors;
using Xunit;

namespace PdfConverter.Tests.Views.Behaviors
{
    /// <summary>
    /// <see cref="FileDropBehavior"/> のドロップ判定と添付プロパティを検証する
    /// </summary>
    public class FileDropBehaviorTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// 有効化すると AllowDrop が true になり、無効化すると false になることを検証する
        /// </summary>
        [Fact]
        public void IsEnabled_TogglesAllowDrop()
        {
            StaTestHelper.Run(() =>
            {
                var window = new Window();
                FileDropBehavior.SetIsEnabled(window, true);
                Assert.True(window.AllowDrop);

                FileDropBehavior.SetIsEnabled(window, false);
                Assert.False(window.AllowDrop);
            });
        }

        /// <summary>
        /// サポート対象ファイルのドラッグ時にコピー効果とオーバーレイ表示が有効になることを検証する
        /// </summary>
        [Fact]
        public void EvaluateDragEffects_SupportedDocument_ReturnsCopyAndShowsOverlay()
        {
            var viewModel = new Mock<IMainWindowViewModel>();
            viewModel.SetupGet(v => v.IsBusy).Returns(false);
            var data = CreateFileDropData(@"C:\sample.pdf");

            DragDropEffects effects = FileDropBehavior.EvaluateDragEffects(
                viewModel.Object,
                data,
                out bool showOverlay);

            Assert.Equal(DragDropEffects.Copy, effects);
            Assert.True(showOverlay);
        }

        /// <summary>
        /// 処理中はドロップを受け付けないことを検証する
        /// </summary>
        [Fact]
        public void EvaluateDragEffects_WhenBusy_ReturnsNone()
        {
            var viewModel = new Mock<IMainWindowViewModel>();
            viewModel.SetupGet(v => v.IsBusy).Returns(true);
            var data = CreateFileDropData(@"C:\sample.pdf");

            DragDropEffects effects = FileDropBehavior.EvaluateDragEffects(
                viewModel.Object,
                data,
                out bool showOverlay);

            Assert.Equal(DragDropEffects.None, effects);
            Assert.False(showOverlay);
        }

        /// <summary>
        /// サポート対象ファイルのドロップ時に ViewModel へ委譲されることを検証する
        /// </summary>
        [Fact]
        public void TryProcessDrop_SupportedDocument_DelegatesToViewModel()
        {
            var viewModel = new Mock<IMainWindowViewModel>();
            viewModel.SetupGet(v => v.IsBusy).Returns(false);
            string filePath = @"C:\sample.docx";
            var data = CreateFileDropData(filePath);

            bool accepted = FileDropBehavior.TryProcessDrop(viewModel.Object, data);

            Assert.True(accepted);
            viewModel.VerifySet(v => v.IsDropOverlayVisible = false);
            viewModel.Verify(v => v.HandleDroppedDocument(filePath), Times.Once);
        }

        /// <summary>
        /// 処理中はドロップを無視することを検証する
        /// </summary>
        [Fact]
        public void TryProcessDrop_WhenBusy_ReturnsFalse()
        {
            var viewModel = new Mock<IMainWindowViewModel>();
            viewModel.SetupGet(v => v.IsBusy).Returns(true);
            var data = CreateFileDropData(@"C:\sample.pdf");

            bool accepted = FileDropBehavior.TryProcessDrop(viewModel.Object, data);

            Assert.False(accepted);
            viewModel.Verify(v => v.HandleDroppedDocument(It.IsAny<string>()), Times.Never);
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        private static IDataObject CreateFileDropData(string filePath)
        {
            var data = new DataObject();
            data.SetData(DataFormats.FileDrop, new[] { filePath });
            return data;
        }
    }
}
