using System.IO;
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
            string filePath = CreateTempPdfPath();

            try
            {
                var viewModel = new Mock<IMainWindowViewModel>();
                viewModel.SetupGet(v => v.IsBusy).Returns(false);
                var data = CreateFileDropData(filePath);

                DragDropEffects effects = FileDropBehavior.EvaluateDragEffects(
                    viewModel.Object,
                    data,
                    out bool showOverlay);

                Assert.Equal(DragDropEffects.Copy, effects);
                Assert.True(showOverlay);
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        /// <summary>
        /// 処理中はドロップを受け付けないことを検証する
        /// </summary>
        [Fact]
        public void EvaluateDragEffects_WhenBusy_ReturnsNone()
        {
            string filePath = CreateTempPdfPath();

            try
            {
                var viewModel = new Mock<IMainWindowViewModel>();
                viewModel.SetupGet(v => v.IsBusy).Returns(true);
                var data = CreateFileDropData(filePath);

                DragDropEffects effects = FileDropBehavior.EvaluateDragEffects(
                    viewModel.Object,
                    data,
                    out bool showOverlay);

                Assert.Equal(DragDropEffects.None, effects);
                Assert.False(showOverlay);
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        /// <summary>
        /// サポート対象ファイルのドロップ時に ViewModel へ正規化済みパスを委譲することを検証する
        /// </summary>
        [Fact]
        public void TryProcessDrop_SupportedDocument_DelegatesToViewModel()
        {
            string filePath = CreateTempDocxPath();

            try
            {
                var viewModel = new Mock<IMainWindowViewModel>();
                viewModel.SetupGet(v => v.IsBusy).Returns(false);
                var data = CreateFileDropData(filePath);

                bool accepted = FileDropBehavior.TryProcessDrop(viewModel.Object, data);

                Assert.True(accepted);
                viewModel.VerifySet(v => v.IsDropOverlayVisible = false);
                viewModel.Verify(v => v.HandleDroppedDocument(Path.GetFullPath(filePath)), Times.Once);
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        /// <summary>
        /// 複数ファイルのドロップ時に先頭の 1 件だけを読み込み、通知することを検証する
        /// </summary>
        [Fact]
        public void TryProcessDrop_MultipleFiles_LoadsFirstAndNotifies()
        {
            string firstPath = CreateTempPdfPath();
            string secondPath = CreateTempPdfPath();

            try
            {
                var viewModel = new Mock<IMainWindowViewModel>();
                viewModel.SetupGet(v => v.IsBusy).Returns(false);
                var data = CreateFileDropData(firstPath, secondPath);

                bool accepted = FileDropBehavior.TryProcessDrop(viewModel.Object, data);

                Assert.True(accepted);
                viewModel.Verify(v => v.HandleDroppedDocument(Path.GetFullPath(firstPath)), Times.Once);
                viewModel.Verify(v => v.HandleDroppedDocument(Path.GetFullPath(secondPath)), Times.Never);
                viewModel.Verify(v => v.NotifyMultipleFilesDropped(2, Path.GetFullPath(firstPath)), Times.Once);
            }
            finally
            {
                File.Delete(firstPath);
                File.Delete(secondPath);
            }
        }

        /// <summary>
        /// 1 件のドロップでは複数ファイルの通知をしないことを検証する
        /// </summary>
        [Fact]
        public void TryProcessDrop_SingleFile_DoesNotNotify()
        {
            string filePath = CreateTempPdfPath();

            try
            {
                var viewModel = new Mock<IMainWindowViewModel>();
                viewModel.SetupGet(v => v.IsBusy).Returns(false);
                var data = CreateFileDropData(filePath);

                FileDropBehavior.TryProcessDrop(viewModel.Object, data);

                viewModel.Verify(v => v.NotifyMultipleFilesDropped(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        /// <summary>
        /// 処理中はドロップを無視することを検証する
        /// </summary>
        [Fact]
        public void TryProcessDrop_WhenBusy_ReturnsFalse()
        {
            string filePath = CreateTempPdfPath();

            try
            {
                var viewModel = new Mock<IMainWindowViewModel>();
                viewModel.SetupGet(v => v.IsBusy).Returns(true);
                var data = CreateFileDropData(filePath);

                bool accepted = FileDropBehavior.TryProcessDrop(viewModel.Object, data);

                Assert.False(accepted);
                viewModel.Verify(v => v.HandleDroppedDocument(It.IsAny<string>()), Times.Never);
            }
            finally
            {
                File.Delete(filePath);
            }
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        private static DataObject CreateFileDropData(params string[] filePaths)
        {
            var data = new DataObject();
            data.SetData(DataFormats.FileDrop, filePaths);
            return data;
        }

        private static string CreateTempPdfPath()
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{System.Guid.NewGuid():N}.pdf");
            File.WriteAllText(path, "placeholder");
            return path;
        }

        private static string CreateTempDocxPath()
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{System.Guid.NewGuid():N}.docx");
            File.WriteAllText(path, "placeholder");
            return path;
        }
    }
}
