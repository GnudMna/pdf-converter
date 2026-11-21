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
            var userSettings = new Mock<IUserSettingsService>();
            var viewModel = new MainViewModel(
                dialog.Object,
                clipboard.Object,
                new PdfPreviewCoordinator(pdf.Object, documentPdfSource.Object),
                new PdfSaveCoordinator(pdf.Object, documentPdfSource.Object, dialog.Object),
                wordToPdfSettings.Object,
                imageExportSettings.Object,
                userSettings.Object);
            return (viewModel, pdf, dialog, clipboard);
        }

        /// <summary>
        /// プレビュー コーディネーターを差し替えた <see cref="MainViewModel"/> を生成する
        /// </summary>
        /// <param name="previewCoordinator">プレビュー コーディネーター</param>
        /// <returns>生成した ViewModel</returns>
        public static MainViewModel Create(IPdfPreviewCoordinator previewCoordinator)
        {
            return new MainViewModel(
                new Mock<IDialogService>().Object,
                new Mock<IClipboardService>().Object,
                previewCoordinator,
                new Mock<IPdfSaveCoordinator>().Object,
                WordToPdfConversionSettingsTestHelper.Create().Object,
                ImageExportSettingsTestHelper.Create().Object,
                new Mock<IUserSettingsService>().Object);
        }
    }
}
