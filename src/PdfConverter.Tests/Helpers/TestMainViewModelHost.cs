using System;
using System.Threading;
using System.Windows.Media.Imaging;
using PdfConverter.Models;
using PdfConverter.ViewModels;

namespace PdfConverter.Tests.Helpers
{
    /// <summary>
    /// Coordinator 単体テスト用の <see cref="IMainViewModelHost"/> 実装
    /// </summary>
    internal sealed class TestMainViewModelHost : IMainViewModelHost
    {
        private CancellationTokenSource _cancellationTokenSource;

        public string FilePath { get; set; }

        public string LoadedFilePath { get; set; }

        public string PageNumber { get; set; } = "1";

        public string PageRange { get; set; } = "1";

        public int PageCount { get; set; }

        public BitmapSource PreviewImage { get; set; }

        public ResolutionMode ResolutionMode { get; set; } = ResolutionMode.Width;

        public string ResolutionValue { get; set; } = "1080";

        public bool PreserveTransparency { get; set; } = true;

        public bool IsAllPagesSelected { get; set; }

        public OutputImageFormat OutputImageFormat { get; set; } = OutputImageFormat.Png;

        public bool IsBusy { get; set; }

        public bool IsSaving { get; set; }

        public string StatusMessage { get; set; }

        public double ProgressValue { get; set; }

        public int CurrentPreviewPage => int.TryParse(PageNumber, out int value)
            ? (value < 1 ? 1 : (value > Math.Max(1, PageCount) ? Math.Max(1, PageCount) : value))
            : 1;

        public void PrepareCancellation()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public CancellationToken GetCancellationToken() => _cancellationTokenSource.Token;

        public void DisposeCancellation()
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        public void RaiseNavigationCanExecuteChanged()
        {
        }

        public void RaiseActionCanExecuteChanged()
        {
        }
    }
}
