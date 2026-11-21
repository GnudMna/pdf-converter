using Moq;
using PdfConverter.Services;
using PdfConverter.ViewModels;

namespace PdfConverter.Tests.Helpers
{
    /// <summary>
    /// <see cref="MainViewModel"/> とモックサービスの生成ヘルパー
    /// </summary>
    internal static class MainViewModelTestFactory
    {
        public static (MainViewModel ViewModel, Mock<IPdfConversionService> Pdf, Mock<IDialogService> Dialog, Mock<IClipboardService> Clipboard) Create()
        {
            var pdf = new Mock<IPdfConversionService>();
            var dialog = new Mock<IDialogService>();
            var clipboard = new Mock<IClipboardService>();
            var viewModel = new MainViewModel(pdf.Object, dialog.Object, clipboard.Object);
            return (viewModel, pdf, dialog, clipboard);
        }
    }
}
