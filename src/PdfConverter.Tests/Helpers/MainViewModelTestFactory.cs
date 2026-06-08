using Moq;
using PdfConverter.Services;
using PdfConverter.Tests.Helpers;
using PdfConverter.ViewModels;
using PdfConverter.ViewModels.Coordinators;

namespace PdfConverter.Tests.Helpers
{
    /// <summary>
    /// <see cref="MainViewModel"/> とモックサービスの生成ヘルパー
    /// </summary>
    internal static class MainViewModelTestFactory
    {
        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        public static (MainViewModel ViewModel, Mock<IPdfConversionService> Pdf, Mock<IDialogService> Dialog, Mock<IClipboardService> Clipboard) Create()
        {
            var pdf = new Mock<IPdfConversionService>();
            var dialog = new Mock<IDialogService>();
            var clipboard = new Mock<IClipboardService>();
            var documentPdfSource = DocumentPdfSourceTestHelper.CreatePassthrough();
            var wordToPdfSettings = WordToPdfConversionSettingsTestHelper.Create();
            var imageExportSettings = ImageExportSettingsTestHelper.Create();
            var viewModel = new MainViewModel(
                dialog.Object,
                clipboard.Object,
                new PdfPreviewCoordinator(pdf.Object, documentPdfSource.Object),
                new PdfSaveCoordinator(pdf.Object, documentPdfSource.Object, dialog.Object),
                wordToPdfSettings.Object,
                imageExportSettings.Object);
            return (viewModel, pdf, dialog, clipboard);
        }
    }
}
