using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Media.Imaging;
using PdfConverter.Models;
using PdfConverter.ViewModels;

namespace PdfConverter.Tests.Helpers
{
    /// <summary>
    /// Coordinator 単体テスト用の <see cref="IMainViewModelHost"/> 実装
    /// </summary>
    internal sealed class TestMainViewModelHost : IMainViewModelHost, IDisposable
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        private CancellationTokenSource _cancellationTokenSource;


        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
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

        public StatusKind StatusKind { get; set; }

        public string ResolutionValidationMessage { get; set; }

        public string PageRangeValidationMessage { get; set; }

        public string PageNumberValidationMessage { get; set; }

        public double ProgressValue { get; set; }

        public bool IsProgressBarVisible { get; set; }

        public int DisplayedPageNumber { get; set; }

        public int CurrentPreviewPage => DisplayedPageNumber > 0
            ? DisplayedPageNumber
            : int.TryParse(PageNumber, out int value)
                ? (value < 1 ? 1 : (value > Math.Max(1, PageCount) ? Math.Max(1, PageCount) : value))
                : 1;

        public int PreviewViewportPixelWidth { get; set; }

        public int PreviewViewportPixelHeight { get; set; }

        /// <summary>パスワードの入力を求められたときの応答 (キャンセル扱いにする場合は <c>null</c> を返す)</summary>
        public Func<string> PasswordResponse { get; set; }

        /// <summary>パスワードの入力を求められた履歴 (直前のパスワードが正しくなかったかどうかを含む)</summary>
        public List<bool> PasswordRequests { get; } = new List<bool>();


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        public string RequestPdfPassword(string filePath, bool previousPasswordWasIncorrect)
        {
            PasswordRequests.Add(previousPasswordWasIncorrect);
            return PasswordResponse?.Invoke();
        }

        public void PrepareCancellation()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public CancellationToken GetCancellationToken() => _cancellationTokenSource.Token;

        /// <summary>進行中の操作をキャンセルする (テスト用)</summary>
        public void Cancel() => _cancellationTokenSource?.Cancel();

        public void DisposeCancellation()
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        public void Dispose() => DisposeCancellation();

        public void RaiseNavigationCanExecuteChanged()
        {
        }

        public void RaiseActionCanExecuteChanged()
        {
        }

        public void SetStatus(string message, StatusKind kind = StatusKind.Info)
        {
            StatusMessage = message;
            StatusKind = kind;
        }

        public void ClearFieldValidationMessages()
        {
            ResolutionValidationMessage = null;
            PageRangeValidationMessage = null;
            PageNumberValidationMessage = null;
        }
    }
}
