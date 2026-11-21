using PdfConverter.Models;
using PdfConverter.ViewModels;
using Xunit;

namespace PdfConverter.Tests.ViewModels
{
    /// <summary>
    /// <see cref="StatusBarViewModel"/> のステータス・進捗表示を検証する
    /// </summary>
    public class StatusBarViewModelTests
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// SetStatus がメッセージと種類をまとめて設定することを検証する
        /// </summary>
        [Fact]
        public void SetStatus_UpdatesMessageAndKind()
        {
            var viewModel = new StatusBarViewModel();

            viewModel.SetStatus("保存完了", StatusKind.Success);

            Assert.Equal("保存完了", viewModel.StatusMessage);
            Assert.Equal(StatusKind.Success, viewModel.StatusKind);
        }

        /// <summary>
        /// プログレスバー表示中かつ保存中でないときに不確定進捗表示になることを検証する
        /// </summary>
        [Fact]
        public void IsProgressIndeterminate_WhenVisibleAndNotSaving_IsTrue()
        {
            var viewModel = new StatusBarViewModel();
            viewModel.IsProgressBarVisible = true;
            viewModel.NotifyBusyStateChanged(isSaving: false);

            Assert.True(viewModel.IsProgressIndeterminate);
        }

        /// <summary>
        /// 保存中は不確定進捗表示にならないことを検証する
        /// </summary>
        [Fact]
        public void IsProgressIndeterminate_WhenSaving_IsFalse()
        {
            var viewModel = new StatusBarViewModel();
            viewModel.IsProgressBarVisible = true;
            viewModel.NotifyBusyStateChanged(isSaving: true);

            Assert.False(viewModel.IsProgressIndeterminate);
        }
    }
}
