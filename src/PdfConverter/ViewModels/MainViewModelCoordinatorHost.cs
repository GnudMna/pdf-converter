using System.Threading;
using System.Windows.Media.Imaging;
using PdfConverter.Models;
using PdfConverter.ViewModels.Coordinators;

namespace PdfConverter.ViewModels
{
    /// <summary>
    /// Coordinator が <see cref="MainViewModel"/> の状態を読み書きするためのアダプター
    /// </summary>
    internal sealed class MainViewModelCoordinatorHost : IMainViewModelHost
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>委譲先のメインビューモデル</summary>
        private readonly MainViewModel _viewModel;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// 指定した <see cref="MainViewModel"/> を Coordinator 向けホストとして公開する
        /// </summary>
        /// <param name="viewModel">委譲先のメインビューモデル</param>
        public MainViewModelCoordinatorHost(MainViewModel viewModel)
        {
            _viewModel = viewModel;
        }


        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <inheritdoc/>
        public string? FilePath
        {
            get => _viewModel.FilePath;
            set => _viewModel.FilePath = value;
        }

        /// <inheritdoc/>
        public string? LoadedFilePath
        {
            get => _viewModel.LoadedFilePath;
            set => _viewModel.LoadedFilePath = value;
        }

        /// <inheritdoc/>
        public string PageNumber
        {
            get => _viewModel.Preview.PageNumber;
            set => _viewModel.Preview.PageNumber = value;
        }

        /// <inheritdoc/>
        public int PageCount
        {
            get => _viewModel.Preview.PageCount;
            set
            {
                _viewModel.Preview.PageCount = value;
                _viewModel.NotifyIsSavePdfVisibleChanged();
            }
        }

        /// <inheritdoc/>
        public BitmapSource? PreviewImage
        {
            get => _viewModel.Preview.PreviewImage;
            set
            {
                _viewModel.Preview.PreviewImage = value;
                _viewModel.NotifyCopyToClipboardCanExecuteChanged();
            }
        }

        /// <inheritdoc/>
        public bool PreserveTransparency => _viewModel.ExportSettings.PreserveTransparency;

        /// <inheritdoc/>
        public int CurrentPreviewPage => _viewModel.Preview.CurrentPreviewPage;

        /// <inheritdoc/>
        public int PreviewViewportPixelWidth => _viewModel.Preview.PreviewViewportPixelWidth;

        /// <inheritdoc/>
        public int PreviewViewportPixelHeight => _viewModel.Preview.PreviewViewportPixelHeight;

        /// <inheritdoc/>
        public string? PageNumberValidationMessage
        {
            get => _viewModel.Preview.PageNumberValidationMessage;
            set => _viewModel.Preview.PageNumberValidationMessage = value;
        }

        /// <inheritdoc/>
        public ResolutionMode ResolutionMode => _viewModel.ExportSettings.ResolutionMode;

        /// <inheritdoc/>
        public string ResolutionValue => _viewModel.ExportSettings.ResolutionValue;

        /// <inheritdoc/>
        public string? ResolutionValidationMessage
        {
            get => _viewModel.ExportSettings.ResolutionValidationMessage;
            set => _viewModel.ExportSettings.ResolutionValidationMessage = value;
        }

        /// <inheritdoc/>
        public string PageRange => _viewModel.ExportSettings.PageRange;

        /// <inheritdoc/>
        public bool IsAllPagesSelected => _viewModel.ExportSettings.IsAllPagesSelected;

        /// <inheritdoc/>
        public OutputImageFormat OutputImageFormat => _viewModel.ExportSettings.OutputImageFormat;

        /// <inheritdoc/>
        public string? PageRangeValidationMessage
        {
            get => _viewModel.ExportSettings.PageRangeValidationMessage;
            set => _viewModel.ExportSettings.PageRangeValidationMessage = value;
        }

        /// <inheritdoc/>
        public bool IsBusy
        {
            get => _viewModel.IsBusy;
            set => _viewModel.IsBusy = value;
        }

        /// <inheritdoc/>
        public bool IsSaving
        {
            get => _viewModel.IsSaving;
            set => _viewModel.IsSaving = value;
        }

        /// <inheritdoc/>
        public double ProgressValue
        {
            get => _viewModel.ProgressValue;
            set => _viewModel.ProgressValue = value;
        }

        /// <inheritdoc/>
        public bool IsProgressBarVisible
        {
            get => _viewModel.IsProgressBarVisible;
            set => _viewModel.IsProgressBarVisible = value;
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <inheritdoc/>
        public string? RequestPdfPassword(string filePath, bool previousPasswordWasIncorrect) =>
            _viewModel.RequestPdfPassword(filePath, previousPasswordWasIncorrect);

        /// <inheritdoc/>
        public void PrepareCancellation() => _viewModel.PrepareCoordinatorCancellation();

        /// <inheritdoc/>
        public CancellationToken GetCancellationToken() => _viewModel.GetCoordinatorCancellationToken();

        /// <inheritdoc/>
        public void DisposeCancellation() => _viewModel.DisposeCoordinatorCancellation();

        /// <inheritdoc/>
        public void SetStatus(string message, StatusKind kind = StatusKind.Info) => _viewModel.SetStatus(message, kind);

        /// <inheritdoc/>
        public void ClearFieldValidationMessages()
        {
            _viewModel.ExportSettings.ClearValidationMessages();
            _viewModel.Preview.PageNumberValidationMessage = null;
        }

        /// <inheritdoc/>
        public void RaiseNavigationCanExecuteChanged() => _viewModel.Preview.RaiseNavigationCanExecuteChanged();

        /// <inheritdoc/>
        public void RaiseActionCanExecuteChanged() => _viewModel.RaiseCoordinatorActionCanExecuteChanged();
    }
}
